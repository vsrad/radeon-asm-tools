#ifndef DISPATCH_HPP__
#define DISPATCH_HPP__

#include <sstream>
#include <cassert>
#include <string>
#include <vector>

#include "hsa.h"
#include "hsa_ext_amd.h"

using namespace std;

struct dispatch_params
{
    uint32_t wg_size[3];
	uint32_t grid_size[3];
	const void* kernarg;
	size_t kernarg_size;
	uint32_t dynamic_lds;
};

struct kernel
{
	uint64_t handle;
	string name;
};

struct CodeObjectHSA
{
	string name;
	hsa_code_object_t code_object;
	hsa_executable_t executable;
	uint64_t kern_obj;
	uint32_t lds_size;
	uint32_t private_size;
};

namespace amd {
namespace dispatch {

class Dispatch
{
private:
	hsa_agent_t agent;
	hsa_agent_t cpu_agent;
	vector<hsa_agent_t> gpu_agents;
	uint32_t queue_size;
	hsa_queue_t* queue;
	hsa_signal_t signal;
	hsa_region_t system_region;
	hsa_region_t kernarg_region;
	hsa_region_t local_region;
	hsa_region_t gpu_local_region;
	hsa_kernel_dispatch_packet_t* aql;
	uint64_t packet_index;
	vector<CodeObjectHSA> cobjects;

	void* kernarg;
	size_t kernarg_size;

	bool CreateSignalsAndQueue();
	bool DestroySignalsAndQueue();
    friend hsa_status_t find_gpu_devices(hsa_agent_t agent, void* data);
	friend hsa_status_t find_regions(hsa_region_t region, void* data);

	void* AllocateKernargMemory(size_t size);

public:
	bool memcpyDtoH(void* dst, const void* src, size_t size) const;
	bool memcpyHtoD(void* dst, const void* src, size_t size) const;

protected:
	Dispatch();

	bool init(uint gpu_id);
	bool shutdown();

	void* allocate_gpumem(size_t size);
	bool free_gpumem(void* ptr);

	void* allocate_cpumem(size_t size);
	bool free_cpumem(void* ptr);

	bool load_kernel_from_memory(kernel* kern, void* bin, size_t size);
	bool run_kernel(const kernel* kern, const dispatch_params* params, uint64_t timeout);
};

} // namespace dispatch
} // namespace amd

#endif // DISPATCH_HPP__
