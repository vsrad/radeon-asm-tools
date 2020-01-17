#include "dispatch.hpp"
#include <string.h>
#include <boost/program_options.hpp>
#include <iostream>
#include <fstream>

using namespace amd::dispatch;
using namespace boost::program_options;

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
    std::string &clang, 
    std::string &asm_source, 
    std::string &include_dir,
    std::string &output_path,
    std::string &debug_path,
    unsigned &debug_size)
    : Dispatch(argc, argv), 
      length(64), 
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

    stream <<"cat  "
      << asm_source << " | "
      << clang << " -x assembler -target amdgcn--amdhsa -mcpu=gfx900 -mno-code-object-v3 -I"
      << include_dir << " -o " << output_path << " -";

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
    SetGridSize(64);
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
    return ok;
  }
};

int main(int argc, const char** argv)
{
  try {
    options_description desc("General options");
    desc.add_options()
    ("help,h", "usage message")
    ("clang", value<std::string>()->default_value("clang"), "clang compiler path")
    ("asm", value<std::string>()->default_value("fp32_v_add.s"), "kernel source")
    ("include", value<std::string>()->default_value("./include/"), "include directories")
    ("output_path", value<std::string>()->default_value("fp32_v_add.co"), "kernel binary output path")
    ("debug_path", value<std::string>()->default_value("debug_result"), "debug buffer binary result")
    ("debug_size", value<unsigned>()->default_value(0), "debug buffer size")
    ;

    variables_map vm;
    store(parse_command_line(argc, argv, desc), vm);

    if (vm.count("help")) {
      std::cout << desc << std::endl;
      return 0;
    }

    std::string clang = vm["clang"].as<std::string>();
    std::string asm_source = vm["asm"].as<std::string>();;
    std::string include_dir = vm["include"].as<std::string>();
    std::string output_path = vm["output_path"].as<std::string>();
    std::string debug_path = vm["debug_path"].as<std::string>();
    unsigned debug_size = vm["debug_size"].as<unsigned>();

    return HalfVectorAdd(argc,
      argv,
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
