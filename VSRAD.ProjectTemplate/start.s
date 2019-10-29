s_load_dword s2, s[4:5], 0x4
s_load_dwordx2 s[0:1], s[6:7], 0x0
s_load_dword s3, s[6:7], 0x8
v_mov_b32_e32 v1, 0
s_waitcnt lgkmcnt(0)
s_and_b32 s2, s2, 0xffff
s_mul_i32 s8, s8, s2
v_add_u32_e32 v0, s8, v0
v_add_u32_e32 v2, s3, v0
v_ashrrev_i64 v[2:3], 30, v[1:2]
v_mov_b32_e32 v0, s1
v_add_co_u32_e32 v2, vcc, s0, v2
v_addc_co_u32_e32 v3, vcc, v0, v3, vcc
global_store_dword v[2:3], v1, off
s_endpgm