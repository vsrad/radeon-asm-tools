#include <string.h>
#include <iostream>
#include <fstream>

#include "dispatch.hpp"
#include "op_params.hpp"

struct vadd_data
{
    uint64_t in1;
    uint64_t in2;
    uint64_t out;
};

bool read_file(const char* path, vector<char>& bin)
{
	std::ifstream file(path, std::ios::binary | std::ios::ate);
	const auto size = file.tellg();
	bool read_failed = false;
	do {
		if (size < 0) { read_failed = true; break; }
		    file.seekg(std::ios::beg);
		if (file.fail()) { read_failed = true; break; }
		    bin.resize(size);
		if (file.rdbuf()->sgetn(bin.data(), size) != size) { read_failed = true; break; }
	} while (false);
	file.close();

	if (read_failed)
		cerr << "unable to read file \"" << path << "\"\n";

	return !read_failed;
}

bool write_file(const char* path, vector<char>& bin)
{
	std::ofstream file(path, std::ios::binary | std::ios::out);
	file.write(bin.data(), bin.size());
	file.close();

    auto write_ok = file.good();

	if (write_ok)
		cerr << "unable to write file \"" << path << "\"\n";

	return write_ok;
}

using namespace std;
using namespace amd::dispatch;

class VectorAdd : public Dispatch
{
private:
    unsigned length;
    int gpu_id;
    int dbg_size;
    string dbg_path;
    string co_path;
    string build_cmd;
    kernel kern;
    vector<float> in1;
    vector<float> in2;
    vector<float> out;
    void *in1_gpu_ptr;
    void *in2_gpu_ptr;
    void *out_gpu_ptr;
    void *dbg_buf_ptr;

public:
    VectorAdd(string &co_path, string &build_cmd, int gpu_id, int dbg_size, string &dbg_path)
        : Dispatch(),
            length(64),
            gpu_id(gpu_id),
            dbg_size(dbg_size),
            dbg_path{std::move(dbg_path)},
            co_path{std::move(co_path)},
            build_cmd{std::move(build_cmd)},
            in1_gpu_ptr{nullptr}, 
            in2_gpu_ptr{nullptr}, 
            out_gpu_ptr{nullptr}, 
            dbg_buf_ptr{nullptr} { }

    bool Init()
    {
        if (!init(gpu_id))
            return false;

        // debug buffer must be allocated before the shader build
        // as debug scripts use ASM_DBG_BUF_ADDR env variable
        if (dbg_size > 0)
        {
            stringstream ss;
            dbg_buf_ptr = allocate_gpumem(dbg_size);

            ss << dbg_buf_ptr;
            auto dbg_ptr_str = ss.str();

            ss.str(""); // clear stream
            ss << dbg_size;
            auto dbg_sze_str = ss.str();

            if ((setenv("ASM_DBG_BUF_ADDR", dbg_ptr_str.c_str(), 1) != 0)
            ||  (setenv("ASM_DBG_BUF_SIZE", dbg_sze_str.c_str(), 1) != 0))
            { 
                cerr << "Failed setup ASM_DBG_BUF_ADDR ASM_DBG_BUF_SIZE env variables\n";
                return false;
            }
        }

        cout << "Execute: " << build_cmd << std::endl;
        if (system(build_cmd.c_str()))
        {
            cerr << "Error: assembly failed \n";
            return false;
        }

        vector<char> bin;
        if (!read_file(co_path.c_str(), bin))
            return false;

        return load_kernel_from_memory(&kern, bin.data(), bin.size());
    }

    bool Setup()
    {
        in1.resize(length);
        in2.resize(length);
        out.resize(length);

        for (unsigned i = 0; i < length; ++i)
        {
            in1[i] = (float)i;
            in2[i] = ((float)i) * 1.25f;
        }

        auto buf_size = length * sizeof(float);
        in1_gpu_ptr = allocate_gpumem(buf_size);
        in2_gpu_ptr = allocate_gpumem(buf_size);

        if (!(memcpyHtoD(in1_gpu_ptr, in1.data(), buf_size)
           && memcpyHtoD(in2_gpu_ptr, in2.data(), buf_size)))
        {
            cerr << "Error: failed to copy to local" << std::endl;
            return false;
        }

        out_gpu_ptr = allocate_gpumem(buf_size);

        vadd_data app_params;
        app_params.in1 = (uint64_t)in1_gpu_ptr;
        app_params.in2 = (uint64_t)in2_gpu_ptr;
        app_params.out = (uint64_t)out_gpu_ptr;

        dispatch_params params;
        params.wg_size[0] = 64;
        params.wg_size[1] = 1;
        params.wg_size[2] = 1;
    
        params.grid_size[0] = params.wg_size[0];
        params.grid_size[1] = params.wg_size[1];
        params.grid_size[2] = params.wg_size[2];

        params.dynamic_lds = 0; 
        params.kernarg_size = sizeof(vadd_data);
        params.kernarg = &app_params;

        return run_kernel(&kern, &params, UINT64_MAX);
    }

    bool Verify()
    {
        if (!memcpyDtoH(out.data(), out_gpu_ptr, out.size() * sizeof(float)))
        {
            cerr << "Error: failed to copy from local" << std::endl;
            return false;
        }

        bool ok = true;
        for (unsigned i = 0; i < length; ++i)
        {
            float f1 = in1[i];
            float f2 = in2[i];
            float res = out[i];
            float expected = f1 + f2;

            if (expected != res)
            {
                cerr << "Error: validation failed at " << i << ": got " << res << " expected " << expected << std::endl;
                ok = false;
            }
        }
        return ok;
    }

    bool Shutdown()
    {
        if (in1_gpu_ptr != nullptr)
        {
            if (!free_gpumem(in1_gpu_ptr))
                cerr << "Failed free in1 buff\n";
        }
        if (in2_gpu_ptr != nullptr)
        {
            if (!free_gpumem(in2_gpu_ptr))
                cerr << "Failed free in2 buff\n";
        }
        if (out_gpu_ptr != nullptr)
        {
            if (!free_gpumem(out_gpu_ptr))
                cerr << "Failed free out buff\n";
        }


        if (dbg_buf_ptr != 0)
        {
            vector<char> bin(dbg_size);

            if (!(memcpyDtoH(bin.data(), dbg_buf_ptr, bin.size()) 
                && free_gpumem(dbg_buf_ptr) 
                && write_file(dbg_path.c_str(), bin)))
                cerr << "Error: failed to copy debug buffer to host\n";
        }

        return shutdown();
    }

    bool Run()
    {
        auto ok = Init()
               && Setup()
               && Verify();

        ok = Shutdown() && ok;
        
        cout << (ok ? "Success" : "Failed") << endl;
        return ok;
    }
};

int main(int argc, const char **argv)
{
    int gpu_id;
    std::string co_path;
    std::string build_cmd;
    int dbg_size;
    std::string dbg_path;

    Options cli_ops(10);

    cli_ops.Add(&gpu_id, "-gpu-id", "", 0, "GPU agent id", atoi);
    cli_ops.Add(&co_path, "-c", "", string(""), "Code object path", str2str);
    cli_ops.Add(&build_cmd, "-asm", "", string(""), "Shader build cmd", str2str);
    cli_ops.Add(&dbg_size, "-bsz", "", 0, "Debug buffer size", atoi);
    cli_ops.Add(&dbg_path, "-b", "", string(""), "Debug buffer path", str2str);

    for (int i = 1; i <= argc - 1; i += 2)
    {
        if (!strcmp(argv[i], "-?") || !strcmp(argv[i], "-help"))
        {
            cli_ops.ShowHelp();
            exit(0);
            return false;
        }

        bool merged_flag = false;
        if (!cli_ops.ProcessArg(argv[i], argv[i + 1], &merged_flag))
        {
            std::cerr << "Unknown flag or flag without value: " << argv[i] << "\n";
            return false;
        }

        if (merged_flag)
        {
            i--;
            continue;
        }

        if (argv[i + 1] && cli_ops.MatchArg(argv[i + 1]))
        {
            std::cerr << "Argument \"" << argv[i + 1]
                      << "\" is aliased with command line flags\n\t maybe real argument is missed for flag \""
                      << argv[i] << "\"\n";
            return false;
        }
    }

    return VectorAdd(co_path, build_cmd, gpu_id, dbg_size, dbg_path).Run();
}
