#include "win32_launcher.h"

/* Small, CRT-free launcher. All application/runtime files stay under app/. */
void *memset(void *target, int value, SIZE_T count) {
    volatile unsigned char *p = (volatile unsigned char *)target;
    while (count--) *p++ = (unsigned char)value;
    return target;
}

static void report_error(const wchar_t *message) {
    HANDLE error = GetStdHandle(STD_ERROR_HANDLE);
    if (error && error != INVALID_HANDLE_VALUE && GetFileType(error) != FILE_TYPE_UNKNOWN) {
        char bytes[2048];
        int size = WideCharToMultiByte(CP_UTF8, 0, message, -1, bytes, sizeof(bytes), NULL, NULL);
        DWORD written;
        if (size > 1) WriteFile(error, bytes, (DWORD)(size - 1), &written, NULL);
    } else MessageBoxW(NULL, message, L"vWheel Hub", MB_OK | MB_ICONERROR);
}

static DWORD launch(void) {
    wchar_t *path = HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, 32768 * sizeof(wchar_t));
    wchar_t *command = HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, 32768 * sizeof(wchar_t));
    if (!path || !command) return ERROR_NOT_ENOUGH_MEMORY;
    DWORD size = GetModuleFileNameW(NULL, path, 32768);
    if (!size || size >= 32768) return ERROR_FILENAME_EXCED_RANGE;
    DWORD end = size;
    while (end && path[end - 1] != L'\\' && path[end - 1] != L'/') end--;
    const wchar_t suffix[] = L"app\\vWheelHub.exe";
    if (!end || end + sizeof(suffix) / sizeof(wchar_t) >= 32768) return ERROR_FILENAME_EXCED_RANGE;
    for (DWORD i = 0; i < sizeof(suffix) / sizeof(wchar_t); i++) path[end + i] = suffix[i];
    /* Command line also needs two quotes and its terminating NUL. */
    if (end + sizeof(suffix) / sizeof(wchar_t) - 1 > 32765) return ERROR_FILENAME_EXCED_RANGE;
    DWORD attributes = GetFileAttributesW(path);
    if (attributes == INVALID_FILE_ATTRIBUTES || attributes & FILE_ATTRIBUTE_DIRECTORY) {
        report_error(L"Cannot find app\\vWheelHub.exe. Restore the complete application folder.\r\n");
        return ERROR_FILE_NOT_FOUND;
    }
    const wchar_t *arguments = GetCommandLineW();
    while (*arguments == L' ' || *arguments == L'\t') arguments++;
    if (*arguments == L'"') {
        arguments++;
        while (*arguments && *arguments != L'"') arguments++;
        if (*arguments) arguments++;
    } else while (*arguments && *arguments != L' ' && *arguments != L'\t') arguments++;
    DWORD used = 0;
    command[used++] = L'"';
    for (DWORD i = 0; path[i]; i++) command[used++] = path[i];
    command[used++] = L'"';
    while (*arguments) {
        if (used >= 32767) return ERROR_FILENAME_EXCED_RANGE;
        command[used++] = *arguments++;
    }
    command[used] = 0;
    STARTUPINFOW startup = {0};
    startup.cb = sizeof(startup);
    startup.dwFlags = STARTF_USESTDHANDLES;
    startup.hStdInput = GetStdHandle(STD_INPUT_HANDLE);
    startup.hStdOutput = GetStdHandle(STD_OUTPUT_HANDLE);
    startup.hStdError = GetStdHandle(STD_ERROR_HANDLE);
    PROCESS_INFORMATION child = {0};
    if (!CreateProcessW(path, command, NULL, NULL, TRUE, 0x08000000, NULL, NULL, &startup, &child)) {
        DWORD error = GetLastError();
        report_error(L"Could not start app\\vWheelHub.exe. Restore the complete application folder.\r\n");
        return error;
    }
    CloseHandle(child.hThread);
    DWORD result = WaitForSingleObject(child.hProcess, INFINITE) == WAIT_OBJECT_0 ? 0 : GetLastError();
    if (!result && !GetExitCodeProcess(child.hProcess, &result)) result = GetLastError();
    CloseHandle(child.hProcess);
    HeapFree(GetProcessHeap(), 0, command);
    HeapFree(GetProcessHeap(), 0, path);
    return result;
}

void WinMainCRTStartup(void) { ExitProcess(launch()); }
