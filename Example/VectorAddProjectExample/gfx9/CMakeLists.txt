add_executable(fp32_v_add fp32_v_add.cpp)
target_link_libraries(fp32_v_add examples_common)
install(FILES ${CMAKE_CURRENT_BINARY_DIR}/fp32_v_add DESTINATION bin COMPONENT examples)