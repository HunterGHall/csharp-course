# 10. Unsafe code & native interop

## What & why

Managed code can't do everything. Sometimes you must call an operating-system API, use a C library (SQLite, OpenSSL, a GPU driver), read a binary file format byte-for-byte, or squeeze the last drop of speed out of a loop. .NET provides **P/Invoke** for calling native libraries, **`unsafe`** code for pointers, and **marshalling** for converting data between worlds. They are powerful and dangerous. A bug here can corrupt memory and crash the process with no exception.

## P/Invoke with `LibraryImport`

To call a function in a native library, declare it `extern` and let the **source-generated** marshaller write the glue (this is the modern, trimming- and AOT-friendly way):

```csharp
using System.Runtime.InteropServices;

static partial class Native
{
    // Windows: int GetTickCount() in kernel32.dll
    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.U4)]
    public static partial uint GetTickCount();

    // POSIX: int getpid(void) in libc
    [LibraryImport("libc", EntryPoint = "getpid")]
    public static partial int GetPid();

    // String parameters need an encoding
    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial int MessageBoxW(nint hWnd, string text, string caption, uint type);
}

Console.WriteLine(Native.GetTickCount());
```

Details:

- The class and method must be `partial`; you write the signature, the generator writes the body.
- `EntryPoint` names the native symbol if it differs from your method name.
- `SetLastError = true` captures the OS error; read it with `Marshal.GetLastPInvokeError()` or `Marshal.GetLastWin32Error()`. Check it immediately and only after failure.
- Older code uses `[DllImport]`, which marshals at run time using reflection-like stubs. `LibraryImport` is preferred for new code.
- To choose a library per platform: use `NativeLibrary.SetDllImportResolver`, conditional compilation (`#if WINDOWS`) or `OperatingSystem.IsWindows()` checks.

### Sizes and types

Native types don't always match C# names. Mind the platform:

| C type | C# type |
| --- | --- |
| `int`, `unsigned int` | `int`, `uint` |
| `long` on Windows / 64-bit Linux | `int` / `long` (**they differ!**); use `CLong`/`CULong` |
| `size_t`, `uintptr_t` | `nuint` |
| `void*`, handles | `nint` / `IntPtr`, or a `SafeHandle` |
| `char*` (UTF-8) | `byte*` / `string` with `StringMarshalling.Utf8` |
| `wchar_t*` (Windows) | `string` with `StringMarshalling.Utf16` |
| `bool` (C `int`) | `int` or `[MarshalAs(UnmanagedType.Bool)] bool` (4 bytes) |

### Structs with a fixed layout

```csharp
[StructLayout(LayoutKind.Sequential)]
struct Point
{
    public int X;
    public int Y;
}

[LibraryImport("user32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
static partial bool GetCursorPos(out Point point);
```

Only **blittable** types (primitives, pointers, structs of them) cross the boundary with zero copying. Arrays, strings and classes require marshalling (copies). Use `[StructLayout(LayoutKind.Explicit)]` with `[FieldOffset]` for unions and exact binary formats; `Pack = 1` for tightly packed structs.

### Callbacks

Native code can call back into C#:

```csharp
// C: void for_each(int* items, int n, void (*callback)(int))
[UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
static void OnItem(int value) => Console.WriteLine(value);

unsafe
{
    delegate* unmanaged[Cdecl]<int, void> fnPtr = &OnItem;
    NativeForEach(items, count, fnPtr);
}
```

The older delegate-based approach (`Marshal.GetFunctionPointerForDelegate`) works, but you **must keep the delegate alive** for as long as native code can call it, or the GC will collect it and the next callback crashes the process.

## Resource handles: `SafeHandle`

Never keep an OS handle as a bare `IntPtr` in your own class, because leaks and use-after-free follow. Wrap it in a `SafeHandle`, which has a finalizer, is reference-counted across P/Invoke calls, and can't be released twice:

```csharp
sealed class SafeFileHandle2 : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeFileHandle2() : base(ownsHandle: true) { }

    protected override bool ReleaseHandle() => CloseHandle(handle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);
}
```

Then declare imports that return `SafeFileHandle2` and use `using`. In practice, use framework wrappers (`FileStream`, `SafeFileHandle`, `Process`) wherever they exist.

## `unsafe` and pointers

Enable it in the project (`<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`) and mark the code `unsafe`:

```csharp
unsafe
{
    int value = 42;
    int* p = &value;          // address-of
    *p = 99;                  // dereference
    Console.WriteLine(value); // 99
}

unsafe int SumFast(int[] data)
{
    int total = 0;
    fixed (int* start = data)           // pin the array so the GC can't move it
    {
        int* p = start;
        int* end = start + data.Length;
        while (p < end) total += *p++;
    }
    return total;
}
```

Key facts:

- The GC may **move** managed objects. `fixed` pins one for the duration of the block; taking a raw pointer to a managed object without pinning is a bug.
- Pointer arithmetic is unchecked: reading past the end silently reads other memory.
- You can only have pointers to *unmanaged* types (no references inside).
- `stackalloc` returns a pointer in an `unsafe` context (or a `Span<T>` in safe code: prefer that).
- `sizeof(T)` and `Unsafe.SizeOf<T>()` give sizes.

**Rule:** try `Span<T>`, `MemoryMarshal`, and `System.Runtime.CompilerServices.Unsafe` first. They give the same performance with far less risk, and most of the time even the span version is plenty.

```csharp
using System.Runtime.CompilerServices;

float f = 1.5f;
int bits = Unsafe.As<float, int>(ref f);        // reinterpret bits (or BitConverter.SingleToInt32Bits)
```

## Native memory

```csharp
unsafe
{
    byte* buffer = (byte*)NativeMemory.Alloc(1024);
    try
    {
        new Span<byte>(buffer, 1024).Clear();
        // ...
    }
    finally
    {
        NativeMemory.Free(buffer);              // you own it: leak it and nobody will reclaim it
    }
}
```

`Marshal.AllocHGlobal`/`FreeHGlobal` do the same; `NativeMemory.AlignedAlloc` handles alignment. Native memory isn't visible to the GC, so cleanup is entirely your job, ideally inside a `SafeHandle` or an `IDisposable` with a finalizer.

## Function pointers

C# can call native or managed functions through typed pointers, avoiding delegate overhead:

```csharp
unsafe
{
    NativeLibrary.TryLoad("libm.so.6", out var lib);
    var cos = (delegate* unmanaged[Cdecl]<double, double>)NativeLibrary.GetExport(lib, "cos");
    Console.WriteLine(cos(0.0));         // 1
}
```

`NativeLibrary.Load`, `TryGetExport` and `SetDllImportResolver` handle dynamic loading and platform-specific names.

## Interop beyond C

- **C++:** wrap it in a flat `extern "C"` C API, since C++ name mangling and classes don't cross P/Invoke. (C++/CLI is Windows-only.)
- **COM** (Windows): `[GeneratedComInterface]` (source-generated) or the older `[ComImport]`.
- **Rust, Go, Zig:** export a C ABI (`extern "C"` / `#[no_mangle]` / `cgo -buildmode=c-shared`) and P/Invoke it.
- **Calling .NET from native:** Native AOT can export C-callable functions with `[UnmanagedCallersOnly(EntryPoint = "add")]`; or host the runtime with `hostfxr`.
- **Python/Java/JavaScript:** common choices are processes plus pipes/HTTP/gRPC rather than in-proc interop.
- **Memory-mapped files** (`MemoryMappedFile`) for sharing data between processes.

## Testing and debugging interop

- Bitness must match: a 64-bit process can't load a 32-bit DLL.
- "DllNotFoundException" or "EntryPointNotFoundException": wrong name, path, architecture, or a missing dependency of the DLL. On Windows use `dumpbin /exports`/Dependencies; on Linux `ldd` and `nm -D`.
- Enable native debugging in your IDE to step into the native code.
- Run native code under sanitizers/valgrind in CI if you own it.
- Wrap every native call in a thin C# method that validates inputs and checks return codes, so unsafety is confined to one file.

## Security

`unsafe` code can bypass every safety guarantee. Treat inputs as hostile: validate lengths before copying, never trust sizes from a file or the network, use checked arithmetic when computing buffer sizes (`checked(a * b)`), and fuzz your parsers.

## Common mistakes

- **Wrong calling convention or struct layout,** which crashes or returns garbage.
- **Assuming C `long` is 64-bit** on every platform.
- **Letting a callback delegate be garbage-collected.**
- **Using a pointer to a managed object outside `fixed`.**
- **Leaking native memory** or freeing it twice.
- **Using `bool` without specifying marshalling** (4-byte Win32 `BOOL` vs 1-byte C `bool`).
- **Ignoring `SetLastError` semantics:** reading the error code after other calls reset it.
- **Marshalling in a hot loop** when blittable types and batching would avoid copies.
- **Reaching for `unsafe`** when a `Span<T>` would be just as fast.

## Exercises

1. Call `getpid` (Linux/macOS) or `GetCurrentProcessId` (Windows) and compare with `Environment.ProcessId`.
2. Write a safe wrapper `static class Native` whose methods validate arguments and convert error codes into exceptions.
3. Implement a `SafeHandle` subclass for an OS handle of your choice and test double-dispose.
4. Rewrite a byte-sum loop with `unsafe` pointers, then with `Span<byte>`, then with `System.Numerics.Vector<byte>`; benchmark all three.
5. Compile a tiny C library (`add.c`) into a shared library and call it with `LibraryImport`; add a callback parameter.
6. Use `MemoryMarshal.Cast` to parse a binary header from a `ReadOnlySpan<byte>` into a `[StructLayout(LayoutKind.Sequential, Pack = 1)]` struct, and verify endianness.
7. Demonstrate that a delegate passed to native code is collected when you don't root it (and then root it).

## Recap

- `LibraryImport` generates fast, AOT-friendly P/Invoke stubs; match native types and layouts exactly.
- Wrap OS handles in `SafeHandle`; keep callbacks alive; free native memory you allocate.
- `unsafe` + `fixed` give pointer access, but prefer `Span<T>`, `MemoryMarshal` and `Unsafe` for most needs.
- Confine unsafe code to thin, well-tested wrappers.

Next: [Packaging, AOT & distribution →](11-packaging-aot-and-distribution.md)
