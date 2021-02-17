#ifndef OP_PARAMS_HPP__
#define OP_PARAMS_HPP__ 1

#include <vector>
#include <cstddef>
#include <iostream>
#include <iomanip>

using std::vector;
using std::string;
using std::size_t;
using std::ostream;
using std::istream;
using std::setw;
using std::cout;

string str2str(const char* s);
float str2f(const char* s);
bool str2b(const char* s);
unsigned int str2u(const char* s);

class CLIFlagBase
{
public:
    bool MatchArg(const char* name);
    const char* MatchMergedArg(const char* name);
    bool ProcessArg(const char* name, const char* val, bool* merged);
    virtual void SetVal(const char* val) = 0;
    virtual void PrintHelp() = 0;
    virtual ~CLIFlagBase() {};

protected:
    CLIFlagBase(const char* name1, const char* name2, const char* comment)
        : name1(name1)
        , name2(name2)
        , comment(comment)
        , count(0) { }

    const char* name1;
    const char* name2;
    const char* comment;
    int count;
};

template <class T>
class CLIFlag : CLIFlagBase
{
public:
    CLIFlag(const char* name1, const char* name2, const char* comment,
        T* ptr, T default_val, const char* default_str, T(*str2val)(const char*))
        : CLIFlagBase(name1, name2, comment)
        , val_ptr(ptr)
        , default_val(default_val)
        , default_str(default_str)
        , str2val(str2val)
    {
        *ptr = default_val;
    }

    virtual void PrintHelp()
    {
        cout << setw(5) << name1 << setw(12) << name2 << setw(10);
        default_str ? (cout << default_str) : (cout << default_val);
        cout << "\t" << comment << "\n";
    }

    virtual void SetVal(const char* val) { *val_ptr = str2val(val); }
    virtual ~CLIFlag() {}

private:
    T* val_ptr;
    T default_val;
    const char* default_str;
    T(*str2val)(const char*); // pointer to StringToValue converter
};

class Options
{
public:
    Options(size_t estimated_sz);
    ~Options();

    bool MatchArg(const char* name);
    bool ProcessArg(const char* name, const char* val, bool* merged);
    bool ParseHeader(const char* s);
    bool ProcessRow(const char* s);
    void ShowHelp();

    template<class T> void Add(T* ptr, const char* name1, const char* name2, T default_val,
        const char* comment, T(*str2val)(const char*))
    {
        auto p = new CLIFlag<T>(name1, name2, comment, ptr, default_val, nullptr, str2val);
        opts.push_back((CLIFlagBase*)p);
    }

    template<class T> void Add(T* ptr, const char* name1, const char* name2, const char* default_str,
        const char* comment, T(*str2val)(const char*))
    {
        T default_val = default_str ? str2val(default_str) : T();
        Add(ptr, name1, name2, default_val, comment, str2val);
    }
    
private:
    CLIFlagBase* ArgPtr(const char* name);

    vector<CLIFlagBase*> opts;
    vector<CLIFlagBase*> idx;
    vector<char> str;
};

#endif