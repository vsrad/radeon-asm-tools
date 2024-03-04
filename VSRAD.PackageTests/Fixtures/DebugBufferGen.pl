use strict;
use warnings;

my $dwords_per_lane = 7;
my @instance_breakpoint_ids = (0, 1);

open my $params_fh, '<', 'DispatchParams.txt' or die;
read $params_fh, my $params, -s $params_fh;
close $params_fh;

my ($grid_size) = $params =~ /grid_size \((\d+),/;
my ($group_size) = $params =~ /group_size \((\d+),/;
my ($wave_size) = $params =~ /wave_size (\d+)/;
my $n_groups = $grid_size / $group_size;

my @buf_data = ();
for (my $gid = 0; $gid < $n_groups; $gid++) {
    for (my $tid = 0; $tid < $group_size; $tid++) {
        my $instance = ($tid / $wave_size) % 2;
        my $buf_offset = ($gid * $group_size + $tid) * $dwords_per_lane;
        if ($tid % $wave_size == 0) {
            $buf_data[$buf_offset + 0] = 0x77777777;
        }
        elsif ($tid % $wave_size == 1) {
            $buf_data[$buf_offset + 0] = $instance_breakpoint_ids[$instance];
        }
        elsif ($tid % $wave_size == 2) {
            $buf_data[$buf_offset + 0] = $instance;
        }
        elsif ($tid % $wave_size == 8 || $tid % $wave_size == 9) {
            $buf_data[$buf_offset + 0] = 0xffffffff;
        }
        else {
            $buf_data[$buf_offset + 0] = 0;
        }
        $buf_data[$buf_offset + 1] = $tid;
        $buf_data[$buf_offset + 2] = $tid / $wave_size; # wave
        $buf_data[$buf_offset + 3] = $tid % $wave_size; # lane
        $buf_data[$buf_offset + 4] = $gid;
        $buf_data[$buf_offset + 5] = $n_groups;
        $buf_data[$buf_offset + 6] = $buf_data[$buf_offset + 2];
    }
}

open my $fh, '>:raw:perlio', "DebugBuffer.bin" or die;
print $fh pack('V*', @buf_data);
close $fh;
