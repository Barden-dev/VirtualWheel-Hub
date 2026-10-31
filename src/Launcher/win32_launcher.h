/* Minimal Windows x64 ABI declarations; no C runtime or third-party headers. */
#ifndef VWH_WIN32_LAUNCHER_H
#define VWH_WIN32_LAUNCHER_H
typedef unsigned long DWORD;
typedef unsigned short WORD;
typedef unsigned char BYTE;
typedef __SIZE_TYPE__ SIZE_T;
typedef __WCHAR_TYPE__ wchar_t;
typedef int BOOL;
typedef void *HANDLE;
#define NULL ((void *)0)
#define INVALID_HANDLE_VALUE ((HANDLE)-1)
#define STD_INPUT_HANDLE ((DWORD)-10)
#define STD_OUTPUT_HANDLE ((DWORD)-11)
#define STD_ERROR_HANDLE ((DWORD)-12)
#define FILE_TYPE_UNKNOWN 0
#define FILE_ATTRIBUTE_DIRECTORY 16
#define INVALID_FILE_ATTRIBUTES ((DWORD)-1)
#define HEAP_ZERO_MEMORY 8
#define CP_UTF8 65001
#define MB_OK 0
#define MB_ICONERROR 16
#define ERROR_NOT_ENOUGH_MEMORY 8
#define ERROR_FILENAME_EXCED_RANGE 206
#define ERROR_FILE_NOT_FOUND 2
#define STARTF_USESTDHANDLES 256
#define INFINITE ((DWORD)-1)
#define WAIT_OBJECT_0 0
#define TRUE 1
typedef struct {
    DWORD cb;
    wchar_t *lpReserved, *lpDesktop, *lpTitle;
    DWORD dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
    WORD wShowWindow, cbReserved2;
    BYTE *lpReserved2;
    HANDLE hStdInput, hStdOutput, hStdError;
} STARTUPINFOW;
typedef struct { HANDLE hProcess, hThread; DWORD dwProcessId, dwThreadId; } PROCESS_INFORMATION;
_Static_assert(sizeof(void *) == 8 && sizeof(DWORD) == 4 && sizeof(wchar_t) == 2, "Windows x64 ABI required");
_Static_assert(sizeof(STARTUPINFOW) == 104 && sizeof(PROCESS_INFORMATION) == 24, "Invalid Win32 structure layout");
#define API __declspec(dllimport)
API HANDLE GetStdHandle(DWORD);
API DWORD GetFileType(HANDLE);
API int WideCharToMultiByte(unsigned int, DWORD, const wchar_t *, int, char *, int, const char *, BOOL *);
API BOOL WriteFile(HANDLE, const void *, DWORD, DWORD *, void *);
API int MessageBoxW(void *, const wchar_t *, const wchar_t *, unsigned int);
API HANDLE GetProcessHeap(void);
API void *HeapAlloc(HANDLE, DWORD, SIZE_T);
API BOOL HeapFree(HANDLE, DWORD, void *);
API DWORD GetModuleFileNameW(HANDLE, wchar_t *, DWORD);
API DWORD GetFileAttributesW(const wchar_t *);
API wchar_t *GetCommandLineW(void);
API BOOL CreateProcessW(const wchar_t *, wchar_t *, void *, void *, BOOL, DWORD, void *, const wchar_t *, STARTUPINFOW *, PROCESS_INFORMATION *);
API BOOL CloseHandle(HANDLE);
API DWORD WaitForSingleObject(HANDLE, DWORD);
API DWORD GetLastError(void);
API BOOL GetExitCodeProcess(HANDLE, DWORD *);
__declspec(dllimport) __attribute__((noreturn)) void ExitProcess(unsigned int);
#endif
