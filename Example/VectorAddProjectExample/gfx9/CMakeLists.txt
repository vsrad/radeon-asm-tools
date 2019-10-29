find_package(Boost 1.40 COMPONENTS program_options REQUIRED)
include_directories(${Boost_INCLUDE_DIR})

add_executable(fp32_v_add fp32_v_add.cpp)
target_link_libraries(fp32_v_add ${Boost_LIBRARIES})
target_link_libraries(fp32_v_add examples_common)
install(FILES ${CMAKE_CURRENT_BINARY_DIR}/fp32_v_add DESTINATION bin COMPONENT examples)