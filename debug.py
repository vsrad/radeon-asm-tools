from optparse import OptionParser
import struct
import random

parser = OptionParser()
parser.add_option("-f", "--file", type="string", dest="input_file")
parser.add_option("-l", "--line", type="int", dest="line")
parser.add_option("-w", "--watches", type="string", dest="watches")
parser.add_option("-o", "--output", type="string", dest="output_file")
parser.add_option("-v", "--vv", type="string", dest="args")
parser.add_option("-t", "--tt", type="int", dest="counter")
parser.add_option("-p", "--pp", type="string", dest="break_args")

(options, args) = parser.parse_args()

def float_to_hex(f):
    return hex(struct.unpack('<I', struct.pack('<f', f))[0])

def read_file_line(line_no):
    with open(options.input_file, 'r') as f:
        line = f.readlines()[line_no]
        if line.endswith('\n'):
            return line
        else:
            return line + '\n'

NUM_GROUPS = 3
num_watches = len(options.watches.split(':'))

with open(options.output_file, 'w+') as f:
    f.write("Breakpoint line: " + str(options.line) + read_file_line(options.line))
    for g in range(0, NUM_GROUPS):
        for i in range(0, 512):
            f.write("0xdeadbeef\n")
            for w in range(0, num_watches):
                f.write(float_to_hex(random.uniform(0.0, 1.0)) + "\n")