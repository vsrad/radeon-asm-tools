from optparse import OptionParser

parser = OptionParser()
parser.add_option("-o", "--out", type="string", dest="out_file")

(options, args) = parser.parse_args()

with open(options.out_file, 'w+') as f:
    f.write('profiling executed normally.')