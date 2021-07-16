.macro PROLOG_KERNEL_DESCRIPTOR kernel_name
    .text
    .globl \kernel_name
    .p2align 8
    .type \kernel_name, @function
    \kernel_name:
.endm

.macro METADATA sc, vc, lds, wg_x, kernel_name
.amdgpu_metadata
---
amdhsa.version: [ 1, 0 ]
amdhsa.kernels:
  - .name: \kernel_name
    .symbol: \kernel_name\().kd
    .language: "OpenCL C"
    .language_version: [ 1, 2 ]
    .sgpr_count: \sc
    .vgpr_count: \vc
    .group_segment_fixed_size: \lds
    .private_segment_fixed_size: 0
    .kernarg_segment_size: 24
    .kernarg_segment_align: 8
    .reqd_workgroup_size: [ \wg_x, 1, 1 ]
    .max_flat_workgroup_size: \wg_x
    .wavefront_size: 64
    .args:
    - { .size: 8, .offset:  0,  .value_kind: global_buffer, .value_type: f32, .name: in1, .address_space: global, .is_const: true }
    - { .size: 8, .offset:  8,  .value_kind: global_buffer, .value_type: f32, .name: in2, .address_space: global, .is_const: true }
    - { .size: 8, .offset:  16, .value_kind: global_buffer, .value_type: f32, .name: out, .address_space: global, .is_const: false }
...
.end_amdgpu_metadata
.endm // METADATA

.altmacro
.macro METADATA_WRAPPER sc, vc, lds, wg_x, kernel_name
    METADATA %\sc, %\vc, %\lds, %\wg_x, \kernel_name
.endm

.macro kernel_end kernel_name
    .Lfunc_end0:
    .size \kernel_name, .Lfunc_end0 - \kernel_name
.endm

.macro EPILOG_KERNEL_DESCRIPTOR kernel_name
    kernel_end \kernel_name

    workgroup_size_x       = 64
    lds_size               = .AUTO_LDS_BYTE_SIZE
    .amdgcn.next_free_sgpr = .AUTO_SGPR_COUNT
    .amdgcn.next_free_vgpr = .AUTO_VGPR_COUNT

    .rodata
    .p2align 6
    .amdhsa_kernel \kernel_name
        .amdhsa_user_sgpr_kernarg_segment_ptr    1
        .amdhsa_system_sgpr_workgroup_id_x       1
        .amdhsa_system_sgpr_workgroup_id_y       0
        .amdhsa_system_sgpr_workgroup_id_z       0
        .amdhsa_system_vgpr_workitem_id          1
        .amdhsa_next_free_vgpr                   .amdgcn.next_free_vgpr
        .amdhsa_next_free_sgpr                   .amdgcn.next_free_sgpr
        .amdhsa_reserve_vcc                      1
        .amdhsa_reserve_xnack_mask               0
        .amdhsa_reserve_flat_scratch             0
    .end_amdhsa_kernel

    total_sgpr_count = .amdgcn.next_free_sgpr + 2 //vcc

    METADATA_WRAPPER total_sgpr_count, .amdgcn.next_free_vgpr, lds_size, workgroup_size_x, <\kernel_name>
.endm

.macro KERNEL_PROLOG kernel_name
	PROLOG_KERNEL_DESCRIPTOR \kernel_name
.endm

.macro KERNEL_EPILOG kernel_name
	EPILOG_KERNEL_DESCRIPTOR \kernel_name
.endm
