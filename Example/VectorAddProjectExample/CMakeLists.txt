cmake_minimum_required(VERSION 2.6)

find_program(CLANG NAMES clang REQUIRED)
if (CLANG)
    message(STATUS "Found Clang: ${CLANG}")
else()
    MESSAGE(STATUS "Clang not found, not building clang examples")
endif()


set (HSA_ROOT "/opt/rocm/hsa" CACHE PATH "HSA runtime path")
set (HSA_HEADER_DIR ${HSA_ROOT}/include/hsa CACHE PATH "HSA include path")
set (ROCM_ROOT "/opt/rocm" CACHE PATH "ROCm runtime path")
set (ROCM_HEADER_DIR ${ROCM_ROOT}/include CACHE PATH "ROCm include path")

find_path(HSA_HEADER hsa.h PATHS ${ROCM_HEADER_DIR} ${HSA_HEADER_DIR} NO_DEFAULT_PATH)
find_path(HSA_HEADER hsa.h)
if (HSA_HEADER)
  MESSAGE(STATUS "Found HSA header: ${HSA_HEADER}/hsa.h")
else (HSA_HEADER)
  MESSAGE(STATUS "HSA header not found. Use -DHSA_HEADER_DIR=<path_to_hsa.h>.")
endif (HSA_HEADER)

find_library(HSA_LIBRARY hsa-runtime64 PATHS ${HSA_LIBRARY_DIR} ${ROCM_ROOT}/lib ${HSA_ROOT}/lib NO_DEFAULT_PATH)
find_library(HSA_LIBRARY hsa-runtime64)
if (HSA_LIBRARY)
  MESSAGE(STATUS "Found HSA library: ${HSA_LIBRARY}")
else (HSA_LIBRARY)
  MESSAGE(STATUS "HSA runtime library not found. Use -DHSA_LIBRARY_DIR=<path_to_libhsa-runtime64.so>.")
endif (HSA_LIBRARY)

if (UNIX)
  set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -Wall -std=c++17")
endif(UNIX)

add_subdirectory(common)
add_subdirectory(gfx9)