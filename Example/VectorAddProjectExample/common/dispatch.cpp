#include <iostream>
#include <fstream>
#include <cstring>
#include <atomic>
#include <cassert>
#include <algorithm>

#include "dispatch.hpp"

using namespace std;

static inline bool check(hsa_status_t status, const char* msg)
{
    if (status == HSA_STATUS_SUCCESS)
        return true;

    const char* err = 0;
    hsa_status_string(status, &err);
    cerr << msg << " failed: " << (err ? err : "hsa_status_string() failed") << endl;

    return false;
}

#define CHECK(x) check(x, #x)

namespace amd {
namespace dispatch {

Dispatch::Dispatch() : queue_size(0), queue(0)
{
    agent.handle = 0;
    cpu_agent.handle = 0;
    signal.handle = 0;
    kernarg_region.handle = 0;
    system_region.handle = 0;
    local_region.handle = 0;
    gpu_local_region.handle = 0;
}

hsa_status_t find_gpu_devices(hsa_agent_t agent, void* data)
{
    if (data == NULL) { return HSA_STATUS_ERROR_INVALID_ARGUMENT; }

    hsa_device_type_t hsa_device_type;
    hsa_status_t status = hsa_agent_get_info(agent, HSA_AGENT_INFO_DEVICE, &hsa_device_type);
    if (status != HSA_STATUS_SUCCESS)
        return status;

    Dispatch* hsa = static_cast<Dispatch*>(data);
    if (hsa_device_type == HSA_DEVICE_TYPE_GPU)
            hsa->gpu_agents.push_back(agent);

    if (hsa_device_type == HSA_DEVICE_TYPE_CPU && hsa->cpu_agent.handle == 0)
            hsa->cpu_agent = agent;

    return HSA_STATUS_SUCCESS;
}

hsa_status_t find_regions(hsa_region_t region, void* data)
{
    hsa_region_segment_t segment_id;

    hsa_region_get_info(region, HSA_REGION_INFO_SEGMENT, &segment_id);
    if (segment_id != HSA_REGION_SEGMENT_GLOBAL)
        return HSA_STATUS_SUCCESS;

    hsa_region_global_flag_t flags;
    bool host_accessible_region = false;
    size_t size, max_alloc, gran, align;
    bool runtime_alloc;
    hsa_region_get_info(region, HSA_REGION_INFO_GLOBAL_FLAGS, &flags);
    hsa_region_get_info(region, (hsa_region_info_t)HSA_AMD_REGION_INFO_HOST_ACCESSIBLE, &host_accessible_region);
    hsa_region_get_info(region, HSA_REGION_INFO_SIZE, &size);
    hsa_region_get_info(region, HSA_REGION_INFO_ALLOC_MAX_SIZE, &max_alloc);
    hsa_region_get_info(region, HSA_REGION_INFO_RUNTIME_ALLOC_ALLOWED, &runtime_alloc);
    hsa_region_get_info(region, HSA_REGION_INFO_RUNTIME_ALLOC_GRANULE, &gran);
    hsa_region_get_info(region, HSA_REGION_INFO_RUNTIME_ALLOC_ALIGNMENT, &align);

    Dispatch* hsa = static_cast<Dispatch*>(data);

    if (flags & HSA_REGION_GLOBAL_FLAG_FINE_GRAINED)
        hsa->system_region = region;

    if (flags & HSA_REGION_GLOBAL_FLAG_COARSE_GRAINED) {
        if (host_accessible_region)
            hsa->local_region = region;
        else
            hsa->gpu_local_region = region;
    }

    if (flags & HSA_REGION_GLOBAL_FLAG_KERNARG)
        hsa->kernarg_region = region;

    return HSA_STATUS_SUCCESS;
}

bool Dispatch::run_kernel(const kernel* kern, const dispatch_params* params, uint64_t timeout)
{
    if (params->kernarg_size > kernarg_size)
    {
        cout << "Recreating kernarg (old size " << kernarg_size
            << ", new size " << params->kernarg_size << ")\n";
        kernarg_size = params->kernarg_size;
        void* new_ptr = AllocateKernargMemory(kernarg_size);
        if(!new_ptr || !CHECK(hsa_memory_free(kernarg)))
            exit(-1);
        kernarg = new_ptr;
    }
    if (params->kernarg)
        memcpy(kernarg, params->kernarg, params->kernarg_size);
    
    const uint32_t queue_mask = queue->size - 1;
    const CodeObjectHSA *co = &cobjects[kern->handle];
    packet_index = hsa_queue_add_write_index_relaxed(queue, 1);
    aql = (hsa_kernel_dispatch_packet_t*)(queue->base_address) + (packet_index & queue_mask);
    memset((uint8_t*)aql + 4, 0, sizeof(*aql) - 4);
    aql->completion_signal = signal;
    aql->workgroup_size_x = params->wg_size[0];
    aql->workgroup_size_y = params->wg_size[1];
    aql->workgroup_size_z = params->wg_size[2];
    aql->grid_size_x = params->grid_size[0];
    aql->grid_size_y = params->grid_size[1];
    aql->grid_size_z = params->grid_size[2];
    aql->private_segment_size = co->private_size;

    uint16_t header =
        (HSA_PACKET_TYPE_KERNEL_DISPATCH << HSA_PACKET_HEADER_TYPE) |
        (1 << HSA_PACKET_HEADER_BARRIER) |
        (HSA_FENCE_SCOPE_SYSTEM << HSA_PACKET_HEADER_ACQUIRE_FENCE_SCOPE) |
        (HSA_FENCE_SCOPE_SYSTEM << HSA_PACKET_HEADER_RELEASE_FENCE_SCOPE);
    uint16_t dim = 1;
    if (aql->grid_size_y > 1)
        dim = 2;
    if (aql->grid_size_z > 1)
        dim = 3;

    aql->group_segment_size = co->lds_size + params->dynamic_lds;
    aql->kernarg_address = kernarg;
    aql->kernel_object = co->kern_obj;

    uint16_t setup = dim << HSA_KERNEL_DISPATCH_PACKET_SETUP_DIMENSIONS;
    uint32_t header32 = header | (setup << 16);
    std::atomic_thread_fence(std::memory_order_release);
#if defined(_WIN32) || defined(_WIN64)  // Windows
    _InterlockedExchange(aql, header32);
#else // Linux
    __atomic_store_n((uint32_t*)aql, header32, __ATOMIC_RELEASE);
#endif
    // Ring door bell
    hsa_signal_store_relaxed(queue->doorbell_signal, packet_index);

    // Wait for completion
    clock_t beg = clock();
    hsa_signal_value_t result;
    do {
        result = hsa_signal_wait_acquire(signal, HSA_SIGNAL_CONDITION_EQ, 0, timeout, HSA_WAIT_STATE_ACTIVE);
        clock_t clocks = clock() - beg;
        if (result != 0 && clocks > (int64_t)timeout * CLOCKS_PER_SEC / 1000) {
            cout << "Kernel execution timed out, elapsed time: " << (long)clocks << endl;
            cout << "Signal value: " << hsa_signal_load_scacquire(signal) << endl;
            
            if (!DestroySignalsAndQueue() || !CreateSignalsAndQueue())
            {
                cout << "Fatal Error: unable to reinitialize signals and queue" << endl;
                exit(-1);
            }
            
            return false;
        }
    } while (result != 0);

    // reset signal
    hsa_signal_store_release(signal, 1);
    return true;
}

bool Dispatch::CreateSignalsAndQueue()
{
    return CHECK(hsa_queue_create(agent, queue_size, HSA_QUEUE_TYPE_MULTI, NULL, NULL, UINT32_MAX, UINT32_MAX, &queue))
        && CHECK(hsa_signal_create(1, 0, NULL, &signal));
}

bool Dispatch::DestroySignalsAndQueue()
{
    return CHECK(hsa_queue_inactivate(queue))
        && CHECK(hsa_signal_destroy(signal))
        && CHECK(hsa_queue_destroy(queue));
}

bool Dispatch::init(uint gpu_id)
{
    cobjects.reserve(30);

    if (!CHECK(hsa_init()) ||
        !CHECK(hsa_iterate_agents(find_gpu_devices, this)))
        return false;

    if (gpu_id >= gpu_agents.size())
    {
        cerr << "unable to detect requested gpu device #" << gpu_id << "\n";
        return false;
    }
    agent = gpu_agents[gpu_id];

    char aname[64] = {};
    if (!(CHECK(hsa_agent_get_info(agent, HSA_AGENT_INFO_NAME, aname))
       && CHECK(hsa_agent_get_info(agent, HSA_AGENT_INFO_QUEUE_MAX_SIZE, &queue_size))
       && CHECK(hsa_agent_iterate_regions(agent, find_regions, this))
       && CreateSignalsAndQueue()))
        return false;

    kernarg_size = 4096;
    kernarg = AllocateKernargMemory(kernarg_size);
    if (!kernarg)
        return false;

    return true;
}

bool Dispatch::shutdown()
{
    return CHECK(hsa_shut_down());
}

void* Dispatch::AllocateKernargMemory(size_t size)
{
    void* p = NULL;
    if (!CHECK(hsa_memory_allocate(kernarg_region, size, (void**)&p)))
        return 0;

    cout << "Allocation at " << p << " kernarg, size 0x" << std::hex << size << " (" << std::dec << size << ")\n";
    return p;
}

hsa_status_t GetKernelName(hsa_executable_t executable, hsa_executable_symbol_t symbol, void* data)
{
    string* kernel_name = (string*)data;
    uint32_t len = 0;

    if (!CHECK(hsa_executable_symbol_get_info(symbol, HSA_EXECUTABLE_SYMBOL_INFO_NAME_LENGTH, &len)))
        return HSA_STATUS_ERROR;

    kernel_name->resize(len);

    return CHECK(hsa_executable_symbol_get_info(symbol, HSA_EXECUTABLE_SYMBOL_INFO_NAME, (void*)kernel_name->c_str()))
        ? HSA_STATUS_SUCCESS : HSA_STATUS_ERROR;
}

static bool delete_co(CodeObjectHSA& co)
{
    bool status = (!co.executable.handle || CHECK(hsa_executable_destroy(co.executable)))
        && (!co.code_object.handle || CHECK(hsa_code_object_destroy(co.code_object)));

    co.code_object.handle = 0;
    co.executable.handle = 0;
    co.name = "deleted";

    return status;
}

bool Dispatch::load_kernel_from_memory(kernel* kern, void* bin, size_t size)
{
    CodeObjectHSA co;
    hsa_executable_symbol_t kern_symbol;
    if (!(CHECK(hsa_code_object_deserialize(bin, size, NULL, &co.code_object))
        && CHECK(hsa_executable_create(HSA_PROFILE_FULL, HSA_EXECUTABLE_STATE_UNFROZEN, NULL, &co.executable))
        && CHECK(hsa_executable_load_code_object(co.executable, agent, co.code_object, NULL))
        && CHECK(hsa_executable_freeze(co.executable, NULL))
        && CHECK(hsa_executable_iterate_symbols(co.executable, GetKernelName, &co.name))
        && CHECK(hsa_executable_get_symbol(co.executable, NULL, co.name.c_str(), agent, 0, &kern_symbol))
        && CHECK(hsa_executable_symbol_get_info(kern_symbol, HSA_EXECUTABLE_SYMBOL_INFO_KERNEL_OBJECT, &co.kern_obj))
        && CHECK(hsa_executable_symbol_get_info(kern_symbol, HSA_EXECUTABLE_SYMBOL_INFO_KERNEL_GROUP_SEGMENT_SIZE, &co.lds_size))
        && CHECK(hsa_executable_symbol_get_info(kern_symbol, HSA_EXECUTABLE_SYMBOL_INFO_KERNEL_PRIVATE_SEGMENT_SIZE, &co.private_size))))
    {
        delete_co(co);
        return false;
    }

    kern->name = co.name;
    kern->handle = cobjects.size();
    cout << "Kernel loaded: " << co.name << "\n";

    cobjects.push_back(co);

    return true;
}

bool Dispatch::memcpyDtoH(void* dst, const void* src, size_t size) const
{
    return CHECK(hsa_memory_copy(dst, src, size));
}

bool Dispatch::memcpyHtoD(void* dst, const void* src, size_t size) const
{
    return CHECK(hsa_memory_copy(dst, src, size));
}

void* Dispatch::allocate_gpumem(size_t size)
{
    void* p = NULL;
    return CHECK(hsa_memory_allocate(gpu_local_region, size, (void**)&p)) ? p : 0;
}

bool Dispatch::free_gpumem(void* ptr)
{
    return CHECK(hsa_memory_free(ptr));
}

void* Dispatch::allocate_cpumem(size_t size)
{
    if (size == 0)
        return NULL;

    void* p = NULL;
    return CHECK(hsa_memory_allocate(system_region, size, (void**)&p)) ? p : 0;
}

bool Dispatch::free_cpumem(void* ptr)
{
    return CHECK(hsa_memory_free(ptr));
}

} // namespace dispatch
} // namespace amd