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

NUM_GROUPS = 27 # 3 * 3 * 3
num_watches = len(options.watches.split(':'))
hidden = 0

try:
    system = int(options.args)
except:
    system = -1

try:
    system1 = int(options.break_args)
except:
    system1 = -1

with open(options.output_file, 'wb') as f:
    f.write(struct.pack("4I", 0, 0, 0, 0)) # TRASH
    for g in range(0, NUM_GROUPS):
        for i in range(0, 512):
            if hidden > 63:
                hidden = 0
            if i == 0:
                f.write(struct.pack("i", system))
            elif i == 1:
                f.write(struct.pack("i", options.counter))
            elif i == 2:
                f.write(struct.pack("i", system1))
            elif i == 8 or i == 9:
                f.write(struct.pack("I", 100))
            else:
                f.write(struct.pack("I", hidden))
            hidden = hidden + 1
            for w in range(0, num_watches):
                f.write(struct.pack("I", (10000 * g + 1000 * w + i)))