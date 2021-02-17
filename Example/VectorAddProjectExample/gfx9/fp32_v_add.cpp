#include "dispatch.hpp"
#include "op_params.hpp"
#include <string.h>
#include <iostream>
#include <fstream>

using namespace amd::dispatch;

class HalfVectorAdd : public Dispatch {
private:
  Buffer* in1;
  Buffer* in2;
  Buffer* out;
  Buffer* debug;
  unsigned length;
  std::string clang;
  std::string asm_source;
  std::string include_dir;
  std::string output_path;
  std::string debug_path;
  unsigned debug_size;

public:
  HalfVectorAdd(int argc, const char **argv,
    unsigned length,
    std::string &clang, 
    std::string &asm_source, 
    std::string &include_dir,
    std::string &output_path,
    std::string &debug_path,
    unsigned &debug_size)
    : Dispatch(argc, argv), 
      length(length), 
      clang{std::move(clang) }, 
      asm_source{std::move(asm_source) }, 
      include_dir{std::move(include_dir) },
      output_path{std::move(output_path) },
      debug_path{std::move(debug_path) },
      debug_size(debug_size) { }

  bool SetupDebugBuffer() override {
    std::stringstream stream;

    if (debug_size) {
      debug = AllocateBuffer(debug_size);
      stream << debug->LocalPtr();
      std::string ptr_string = stream.str();
      if (setenv("ASM_DBG_BUF_ADDR", ptr_string.c_str(), 1)) { output << "Error: ASM_DBG_BUF_ADDR setup failed" << std::endl; return false; }
    }
    
    return true;
  }

  bool SetupCodeObject() override {
    std::stringstream stream;

    std::string agent_name;
    if (!GetAgentName(agent_name)) return false;

    stream <<"cat  "
      << asm_source << " | "
      << clang << " -x assembler -target amdgcn--amdhsa -mno-code-object-v3 -I"
      << include_dir << " -mcpu=" << agent_name << " -o " << output_path << " -";

    std::string clang_call = stream.str();

    output << "Execute: " << clang_call << std::endl;
    if (system(clang_call.c_str())) { output << "Error: kernel build failed - " << asm_source << std::endl; return false; }

    return LoadCodeObjectFromFile(output_path);
  }

  bool DumpDebugBuffer() {
    if (debug_size) {
      if (!CopyFrom(debug)) { output << "Error: failed to copy debug buffer from local" << std::endl; return false; }

      std::ofstream fs(debug_path, std::ios::out | std::ios::binary);
      if (!fs.is_open()) { output << "Error: failed to open " << debug_path << std:: endl; return false; }

      fs.write(debug->Ptr<char>(), debug->Size());
      fs.close();

      FreeBuffer(debug);
    }

    return true;
  }

  bool Setup() override {
    if (!AllocateKernarg(3 * sizeof(Buffer*))) { return false; }
    in1 = AllocateBuffer(length * sizeof(float));
    in2 = AllocateBuffer(length * sizeof(float));
    for (unsigned i = 0; i < length; ++i) {
      in1->Data<float>(i) = (float)i;
      in2->Data<float>(i) = ((float)i) * 1.25f;
    }
    if (!CopyTo(in1)) { output << "Error: failed to copy to local" << std::endl; return false; }
    if (!CopyTo(in2)) { output << "Error: failed to copy to local" << std::endl; return false; }
    out = AllocateBuffer(length * sizeof(float));

    Kernarg(in1);
    Kernarg(in2);
    Kernarg(out);
    SetGridSize(length);
    SetWorkgroupSize(64);
    return true;
  }

  bool Verify() override {
    if (!CopyFrom(out)) { output << "Error: failed to copy from local" << std::endl; return false; }
    if (!DumpDebugBuffer()) { output << "Error: failed to dump debug buffer" << std::endl; return false; }

    bool ok = true;
    for (unsigned i = 0; i < length; ++i) {
      float f1 = in1->Data<float>(i);
      float f2 = in2->Data<float>(i);
      float res = out->Data<float>(i);
      float expected = f1 + f2;
      if (expected != res){
        output << "Error: validation failed at " << i << ": got " << res << " expected " << expected << std::endl;
        ok = false;
      }
    }

    FreeBuffer(in1);
    FreeBuffer(in2);
    FreeBuffer(out);
    return ok;
  }
};

int main(int argc, const char** argv)
{
  try {

    std::string clang;
    std::string asm_source;
    std::string include_dir;
    std::string output_path;
    std::string debug_path;
    unsigned int debug_size;
    unsigned int length;

    Options cli_ops(100);
    cli_ops.Add(&clang,  "-asm", "", string("/opt/rocm/llvm/bin/clang"), "path to compiler", str2str);
    cli_ops.Add(&asm_source,  "-s", "", string(""), "path to source", str2str);
    cli_ops.Add(&include_dir,  "-I", "", string(""), "path to include dir", str2str);
    cli_ops.Add(&output_path,  "-o", "", string(""), "path to output code object", str2str);
    cli_ops.Add(&debug_path,  "-b", "", string(""), "path to debug buffer", str2str);
    cli_ops.Add(&debug_size,  "-bsz", "", 0u, "debug buffer size", str2u);
    cli_ops.Add(&length,  "-l", "", 64u, "vector length", str2u);

    for (int i = 1; i <= argc-1; i += 2)
    {
        if (!strcmp(argv[i], "-?") || !strcmp(argv[i], "-help"))
        {
            cli_ops.ShowHelp();
            exit(0);
            return false;
        }

        bool merged_flag = false;
        if (!cli_ops.ProcessArg(argv[i], argv[i+1], &merged_flag))
        {
            std::cerr << "Unknown flag or flag without value: " << argv[i] << "\n";
            return false;
        }

        if (merged_flag)
        {
            i--;
            continue;
        }

        if (argv[i+1] && cli_ops.MatchArg(argv[i+1]))
        {
            std::cerr << "Argument \"" << argv[i + 1]
                << "\" is aliased with command line flags\n\t maybe real argument is missed for flag \""
                << argv[i] << "\"\n";
            return false;
        }
    }

    if (clang.empty() || asm_source.empty() || include_dir.empty() || output_path.empty())
    {
        std::cerr << "Some argument is empty\n";
        cli_ops.ShowHelp();
        exit(0);
        return false;
    }

    return HalfVectorAdd(argc,
      argv,
      length,
      clang,
      asm_source,
      include_dir,
      output_path,
      debug_path,
      debug_size).RunMain();
  }
  catch (std::exception& e) {
    std::cerr << e.what() << std::endl;
  }
}
