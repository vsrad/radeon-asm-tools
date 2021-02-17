#include "op_params.hpp"
#include <iostream>
#include <string.h>
#include <string>
#include <array>

using std::array;

string       str2str(const char* s) { return string(s); }
float        str2f(const char* s)   { return atof(s); }
bool         str2b(const char* s)   { return atoi(s); }
unsigned int str2u(const char* s)   { return atol(s); }

const char* CLIFlagBase::MatchMergedArg(const char* name)
{
    auto len = strlen(name1);
    auto arglen = strlen(name);
    bool match = len && arglen > len && !strncmp(name, name1, len) && name[len] >= '0' && name[len] <= '9';

    return match ? &name[len] : nullptr;
}

bool CLIFlagBase::MatchArg(const char* name)
{
    if (strcmp(name, name1) == 0 || strcmp(name, name2) == 0)
        return true;

    if (strlen(name) < 2 || name[0] != '-')
        return false;

    return strcmp(name + 1, name1) == 0 || strcmp(name + 1, name2) == 0;
}

bool CLIFlagBase::ProcessArg(const char* name, const char* val, bool* merged)
{
    if (!MatchArg(name))
    {
        val = MatchMergedArg(name);
        if (val && merged)
            *merged = true;
    }
    
    if (!val)
        return false;

    this->SetVal(val);
    return true;
}

Options::Options(size_t estimated_sz)
{
    opts.reserve(estimated_sz);
}

Options::~Options()
{
    for (auto& opt : opts)
        delete opt;
}

CLIFlagBase* Options::ArgPtr(const char* name)
{
    for (auto& opt : opts)
        if (opt->MatchArg(name))
            return opt;

    return nullptr;
}

bool Options::MatchArg(const char* name)
{
    for (auto& opt : opts)
        if (opt->MatchArg(name) || opt->MatchMergedArg(name))
            return true;

    return false;
}

bool Options::ProcessArg(const char* name, const char* val, bool* merged)
{
    for (auto& opt : opts)
        if (opt->ProcessArg(name, val, merged))
            return true;

    return false;
}

void Options::ShowHelp()
{
    for (auto& opt : opts)
        opt->PrintHelp();
}

bool Options::ParseHeader(const char* s)
{
    str.resize(strlen(s)+1);
    strcpy(str.data(), s);
    idx.clear();
    
    char* t = strtok(str.data(), " \t");
    while (t != NULL)
    {
        CLIFlagBase* p = ArgPtr(t);

        if (!p)
        {
            std::cerr << "Unable to parse header parameter \"" << t << "\"\n";
            return false;
        }

        idx.push_back(p);
        t = strtok(NULL, " \t");
    }

    return true;
}

bool Options::ProcessRow(const char* s)
{
    str.resize(strlen(s) + 1);
    strcpy(str.data(), s);

    char* t = strtok(str.data(), " \t");
    size_t i = 0;
    for (i = 0; t; i++)
    {
        if (i >= idx.size())
            return false;

        idx[i]->SetVal(t);
        t = strtok(NULL, " \t");
    }

    return i == idx.size();
}

