//
// Vector add example using fp32 storage data type and fp32 add instruction
//

.include "gpr_alloc.inc"

.hsa_code_object_version 2,1
.hsa_code_object_isa

.GPR_ALLOC_BEGIN
    kernarg = 0
    gid_x = 2
    .SGPR_ALLOC_FROM 5
    .SGPR_ALLOC tmp
    .SGPR_ALLOC base_in1, 2
    .SGPR_ALLOC base_in2, 2
    .SGPR_ALLOC base_out, 2

    .VGPR_ALLOC_FROM 0
    .VGPR_ALLOC tid
    .VGPR_ALLOC voffset
    .VGPR_ALLOC vaddr, 2
    .VGPR_ALLOC in1
    .VGPR_ALLOC in2
    .VGPR_ALLOC out
.GPR_ALLOC_END


.text
.p2align 8
.amdgpu_hsa_kernel hello_world

hello_world:

    .amd_kernel_code_t
        is_ptr64 = 1
        enable_sgpr_kernarg_segment_ptr = 1
        enable_sgpr_workgroup_id_x = 1
        kernarg_segment_byte_size = 24
        compute_pgm_rsrc2_user_sgpr = 2
        granulated_workitem_vgpr_count = .AUTO_VGPR_GRANULATED_COUNT
        granulated_wavefront_sgpr_count = .AUTO_SGPR_GRANULATED_COUNT
        wavefront_sgpr_count = .AUTO_SGPR_COUNT
        workitem_vgpr_count = .AUTO_VGPR_COUNT
    .end_amd_kernel_code_t

  // read kernel arguments:
  // s[base_in1:base_in1+1] = *in1
  // s[base_in2:base_in2+1] = *in2
  // s[base_out:base_out+1] = *out
  s_load_dwordx2        s[base_in1:base_in1+1], s[kernarg:kernarg+1], 0x00
  s_load_dwordx2        s[base_in2:base_in2+1], s[kernarg:kernarg+1], 0x08
  s_load_dwordx2        s[base_out:base_out+1], s[kernarg:kernarg+1], 0x10
  

  // group offset (group size 64)
  s_mul_i32             s[tmp], s[gid_x], 64
  v_add_u32             v[voffset], v[tid], s[tmp]
  v_lshlrev_b32         v[voffset], 2, v[voffset]
  s_waitcnt             0
  
  // vaddr = &in1[i]
  v_add_co_u32          v[vaddr], vcc, s[base_in1], v[voffset]
  v_mov_b32             v[vaddr+1], s[base_in1+1]
  v_addc_co_u32         v[vaddr+1], vcc, v[vaddr+1], 0, vcc
  flat_load_dword       v[in1], v[vaddr:vaddr+1]

  // vaddr = &in2[i]
  v_add_co_u32          v[vaddr], vcc, s[base_in2], v[voffset]
  v_mov_b32             v[vaddr+1], s[base_in2+1]
  v_addc_co_u32         v[vaddr+1], vcc, v[vaddr+1], 0, vcc
  flat_load_dword       v[in2], v[vaddr:vaddr+1]
  
  // vaddr = &out[i]
  v_add_co_u32          v[vaddr], vcc, s[base_out], v[voffset]
  v_mov_b32             v[vaddr+1], s[base_out+1]
  v_addc_co_u32         v[vaddr+1], vcc, v[vaddr+1], 0, vcc
  
  // wait for memory operations to complete
  s_waitcnt             0
  
  v_add_f32             v[out], v[in1], v[in2]
  
  flat_store_dword      v[vaddr:vaddr+1], v[out]
  s_endpgm
