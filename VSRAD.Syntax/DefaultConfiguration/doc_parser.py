'''
This script parses the documentation https://llvm.org/docs/AMDGPU/ 
and writes to a file in the documentation format for Rad Asm Syntax extension.

Prerequisites:
    * install BeautifulSoup https://www.crummy.com/software/BeautifulSoup/bs4/doc/#installing-beautiful-soup
    * install tabulate https://pypi.org/project/tabulate/
'''

import re
import os
import collections
import urllib.request
from tabulate import tabulate

try: 
    from BeautifulSoup import BeautifulSoup
except ImportError:
    from bs4 import BeautifulSoup

INSTRUCTION_REGEX = re.compile('^[a-z_0-9]+', flags=re.IGNORECASE)
EOL = '\n'

def getPageByUrl(url):
    fp = urllib.request.urlopen(url)
    
    pageStr = fp.read().decode('utf-8')
    fp.close()

    return pageStr

def isValidInstruction(instruction):
    instruction = instruction.rstrip()

    if instruction.endswith('_sdwa'):
        return False
    elif instruction.endswith('_dpp'):
        return False
    elif instruction.endswith('_e64'):
        return False
    return True

def replaceEolWithSpace(strr):
    return " ".join(strr.splitlines())

def getTagText(tag):
    return replaceEolWithSpace(tag.text)

def parseTagOrGetText(tag):
    aStr = ""
    childTags = tag.findChildren(recursive=False)
    if (any(x.name != "a" and x.name != "em" for x in childTags)):
        for childTag in childTags:
            aStr += parseTag(childTag)
    else:
        aStr += "%s %s" % (getTagText(tag), EOL)
        
    return aStr

def parseTag(tag):
    aStr = ""

    if (tag.name == "p"):
        if (getTagText(tag) != "Examples:"):
            aStr += "%s %s" % (getTagText(tag), EOL)

    elif (tag.name == "blockquote"):
        aStr += EOL
        header = [getTagText(thTag) for thTag in tag.find("thead").find("tr").findChildren("th")]
        rows = []
        for trTag in tag.find("tbody").findChildren("tr"):
            data = [parseTagOrGetText(tdTag) for tdTag in trTag.findChildren("td")]
            rows.append(data)

        tableLines = tabulate(rows, headers=header).splitlines(True)
        formattedTableLines = ["  %s" % line for line in tableLines]
        aStr += "".join(formattedTableLines) + EOL
        aStr += EOL

    elif (tag.name == "ul"):
        aStr += EOL
        for liTag in tag.findChildren("li"):
            parsedLines = parseTag(liTag).splitlines(True)
            for i, line in enumerate(parsedLines):
                if i == 0:
                    aStr += " - " + line
                else:
                    aStr += "   " + line
        aStr += EOL

    elif (tag.name == "li"):
        aStr += parseTagOrGetText(tag)
        
    return aStr

def getAttributeDescription(url):
    aStr = ""
    pageStr = getPageByUrl(url)
    soup = BeautifulSoup(pageStr, "html.parser")
    link = url[url.index('#') + 1:]

    # Just because for some attributes the span id does not match the name of attribute
    # but in such elements, the div id corresponds to the name of the attribute.
    try:
        descriptionTags = soup.find('span', {'id': link}).find_parent("div").findChildren(recursive=False)
    except:
        descriptionTags = soup.find('div', {'id': link}).findChildren(recursive=False)

    for tag in descriptionTags:
        aStr += parseTag(tag)

    aStr = "".join([" * %s" % line for line in aStr.splitlines(True)])
    return aStr

def getInstructionWithDescription(instructionDict, attributeDict, row, docRootUrl):
    instructionSearchResult = INSTRUCTION_REGEX.search(row)

    if instructionSearchResult:
        instructionString = instructionSearchResult.group(0)

        if (isValidInstruction(instructionString)):
            iStr = instructionString
            attribString = row[instructionSearchResult.end():].strip()
            iStr += row[instructionSearchResult.end():row.index(attribString)]

            parsed_html = BeautifulSoup(attribString, "html.parser")
            parsed_references = parsed_html.find_all('a')

            idxSearch = 0
            for i in range(len(parsed_references)):
                reference = parsed_references[i]
                description_url = docRootUrl + reference.get('href')
                
                attributeName = reference.span.text
                iStr += attributeName
                if (i < len(parsed_references) - 1):
                    indexStart = attribString.index(str(parsed_references[i]), idxSearch) + len(str(parsed_references[i]))
                    indexEnd = attribString.index(str(parsed_references[i + 1]), indexStart)
                    idxSearch = indexEnd

                    space = attribString[indexStart:indexEnd]
                    iStr += space

                if (attributeName not in attributeDict):
                    aStr = "/* %s" % EOL
                    aStr += getAttributeDescription(description_url)
                    aStr += " * %s" % EOL
                    aStr += " * %s %s" % (description_url, EOL)
                    aStr += " */%s" % EOL
                    aStr += "let %s %s" % (attributeName, EOL)

                    attributeDict[attributeName] = aStr
            
            instructionDict[instructionString] = iStr

def parseDocumentation(doc_root_url, input_file, output_filename):
    if os.path.exists(output_filename):
        os.remove(output_filename)

    with open(output_filename, 'a') as out_file:
        instructionDict = dict()
        attributeDict = dict()

        for line in input_file.splitlines():
            if line and line.strip():
                getInstructionWithDescription(instructionDict, attributeDict, line, doc_root_url)

        # sort attributes by name
        for k, v in collections.OrderedDict(sorted(attributeDict.items())).items():
            out_file.write(v + EOL)
        for k, v in instructionDict.items():
            out_file.write(v + EOL)

parseDocumentation("https://llvm.org/docs/AMDGPU/", getPageByUrl('https://llvm.org/docs/AMDGPU/AMDGPUAsmGFX8.html'), 'gfx8.radasm1')
parseDocumentation("https://llvm.org/docs/AMDGPU/", getPageByUrl('https://llvm.org/docs/AMDGPU/AMDGPUAsmGFX9.html'), 'gfx9.radasm1')
parseDocumentation("https://llvm.org/docs/AMDGPU/", getPageByUrl('https://llvm.org/docs/AMDGPU/AMDGPUAsmGFX10.html'), 'gfx10.radasm1')