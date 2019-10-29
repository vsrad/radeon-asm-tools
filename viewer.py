from optparse import OptionParser

parser = OptionParser()
parser.add_option("-l", "--local_file", type="string", dest="loc_file")

(options, args) = parser.parse_args()

print('Hello!')
input()

with open(options.loc_file, 'r') as f:
    print(f.read())

input()