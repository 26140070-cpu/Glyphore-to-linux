using System.Runtime.InteropServices;
using System.Text;

namespace Glyphore;

internal static class NativeGl
{
    public const uint GL_VERTEX_SHADER = 0x8B31;
    public const uint GL_FRAGMENT_SHADER = 0x8B30;
    public const uint GL_COMPILE_STATUS = 0x8B81;
    public const uint GL_LINK_STATUS = 0x8B82;
    public const uint GL_INFO_LOG_LENGTH = 0x8B84;
    public const uint GL_TRIANGLES = 0x0004;
    public const uint GL_TEXTURE_2D = 0x0DE1;
    public const uint GL_TEXTURE0 = 0x84C0;
    public const uint GL_TEXTURE_MIN_FILTER = 0x2801;
    public const uint GL_TEXTURE_MAG_FILTER = 0x2800;
    public const uint GL_NEAREST = 0x2600;
    public const uint GL_LINEAR = 0x2601;
    public const uint GL_RGBA = 0x1908;
    public const uint GL_BGRA = 0x80E1;
    public const uint GL_UNSIGNED_BYTE = 0x1401;
    public const uint GL_RGBA8 = 0x8058;
    public const uint GL_COLOR_BUFFER_BIT = 0x00004000;
    public const uint GL_FRAMEBUFFER = 0x8D40;
    public const uint GL_FRAMEBUFFER_BINDING = 0x8CA6;
    public const uint GL_COLOR_ATTACHMENT0 = 0x8CE0;
    public const uint GL_FRAMEBUFFER_COMPLETE = 0x8CD5;
    public const uint GL_BLEND = 0x0BE2;
    public const uint GL_ONE = 1;
    public const uint GL_SRC_ALPHA = 0x0302;
    public const uint GL_ONE_MINUS_SRC_ALPHA = 0x0303;
    public const uint GL_PACK_ALIGNMENT = 0x0D05;
    public const uint GL_UNPACK_ALIGNMENT = 0x0CF5;
    public const uint GL_RENDERER = 0x1F01;
    public const uint GL_VERSION = 0x1F02;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate uint GlCreateShader(uint shaderType);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public unsafe delegate void GlShaderSource(uint shader, int count, byte** strings, int* lengths);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlCompileShader(uint shader);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlGetShaderiv(uint shader, uint pname, out int param);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public unsafe delegate void GlGetShaderInfoLog(uint shader, int maxLength, out int length, byte* infoLog);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate uint GlCreateProgram();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlAttachShader(uint program, uint shader);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlLinkProgram(uint program);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlGetProgramiv(uint program, uint pname, out int param);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public unsafe delegate void GlGetProgramInfoLog(uint program, int maxLength, out int length, byte* infoLog);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlDeleteShader(uint shader);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlDeleteProgram(uint program);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlUseProgram(uint program);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public unsafe delegate void GlGenVertexArrays(int n, uint* arrays);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlDeleteVertexArrays(int n, ref uint arrays);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlBindVertexArray(uint array);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int GlGetUniformLocation(uint program, [MarshalAs(UnmanagedType.LPStr)] string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlUniform1f(int loc, float v0);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlUniform1i(int loc, int v0);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlUniform2f(int loc, float v0, float v1);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlUniform2i(int loc, int v0, int v1);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlUniform3f(int loc, float v0, float v1, float v2);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public unsafe delegate void GlGenTextures(int n, uint* textures);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlBindTexture(uint target, uint texture);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlActiveTexture(uint texture);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlTexParameteri(uint target, uint pname, int param);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlPixelStorei(uint pname, int param);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlTexImage2D(uint target, int level, int internalFormat, int width, int height, int border, uint format, uint type, IntPtr pixels);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public unsafe delegate void GlGenFramebuffers(int n, uint* framebuffers);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlBindFramebuffer(uint target, uint framebuffer);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlFramebufferTexture2D(uint target, uint attachment, uint textarget, uint texture, int level);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate uint GlCheckFramebufferStatus(uint target);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlDeleteFramebuffers(int n, ref uint framebuffers);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlDeleteTextures(int n, ref uint textures);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlViewport(int x, int y, int width, int height);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlDrawArrays(uint mode, int first, int count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlReadPixels(int x, int y, int width, int height, uint format, uint type, IntPtr data);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlFinish();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate IntPtr GlGetString(uint name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlGetIntegerv(uint pname, out int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlClearColor(float r, float g, float b, float a);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlClear(uint mask);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlEnable(uint cap);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlDisable(uint cap);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlBlendFunc(uint sfactor, uint dfactor);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GlBlendFuncSeparate(uint srcRgb, uint dstRgb, uint srcAlpha, uint dstAlpha);

    public static GlCreateShader CreateShader = null!;
    public static GlShaderSource ShaderSource = null!;
    public static GlCompileShader CompileShader = null!;
    public static GlGetShaderiv GetShaderiv = null!;
    public static GlGetShaderInfoLog GetShaderInfoLog = null!;
    public static GlCreateProgram CreateProgram = null!;
    public static GlAttachShader AttachShader = null!;
    public static GlLinkProgram LinkProgram = null!;
    public static GlGetProgramiv GetProgramiv = null!;
    public static GlGetProgramInfoLog GetProgramInfoLog = null!;
    public static GlDeleteShader DeleteShader = null!;
    public static GlDeleteProgram DeleteProgram = null!;
    public static GlUseProgram UseProgram = null!;
    public static GlGenVertexArrays GenVertexArrays = null!;
    public static GlDeleteVertexArrays DeleteVertexArrays = null!;
    public static GlBindVertexArray BindVertexArray = null!;
    public static GlGetUniformLocation GetUniformLocation = null!;
    public static GlUniform1f Uniform1f = null!;
    public static GlUniform1i Uniform1i = null!;
    public static GlUniform2f Uniform2f = null!;
    public static GlUniform2i Uniform2i = null!;
    public static GlUniform3f Uniform3f = null!;
    public static GlGenTextures GenTextures = null!;
    public static GlBindTexture BindTexture = null!;
    public static GlActiveTexture ActiveTexture = null!;
    public static GlTexParameteri TexParameteri = null!;
    public static GlPixelStorei PixelStorei = null!;
    public static GlTexImage2D TexImage2D = null!;
    public static GlGenFramebuffers GenFramebuffers = null!;
    public static GlBindFramebuffer BindFramebuffer = null!;
    public static GlFramebufferTexture2D FramebufferTexture2D = null!;
    public static GlCheckFramebufferStatus CheckFramebufferStatus = null!;
    public static GlDeleteFramebuffers DeleteFramebuffers = null!;
    public static GlDeleteTextures DeleteTextures = null!;
    public static GlViewport Viewport = null!;
    public static GlDrawArrays DrawArrays = null!;
    public static GlReadPixels ReadPixels = null!;
    public static GlFinish Finish = null!;
    public static GlGetString GetString = null!;
    public static GlGetIntegerv GetIntegerv = null!;
    public static GlClearColor ClearColor = null!;
    public static GlClear Clear = null!;
    public static GlEnable Enable = null!;
    public static GlDisable Disable = null!;
    public static GlBlendFunc BlendFunc = null!;
    public static GlBlendFuncSeparate BlendFuncSeparate = null!;

    public static void Load(Func<string, IntPtr> getProcAddress)
    {
        CreateShader = LoadProc<GlCreateShader>(getProcAddress, "glCreateShader");
        ShaderSource = LoadProc<GlShaderSource>(getProcAddress, "glShaderSource");
        CompileShader = LoadProc<GlCompileShader>(getProcAddress, "glCompileShader");
        GetShaderiv = LoadProc<GlGetShaderiv>(getProcAddress, "glGetShaderiv");
        GetShaderInfoLog = LoadProc<GlGetShaderInfoLog>(getProcAddress, "glGetShaderInfoLog");
        CreateProgram = LoadProc<GlCreateProgram>(getProcAddress, "glCreateProgram");
        AttachShader = LoadProc<GlAttachShader>(getProcAddress, "glAttachShader");
        LinkProgram = LoadProc<GlLinkProgram>(getProcAddress, "glLinkProgram");
        GetProgramiv = LoadProc<GlGetProgramiv>(getProcAddress, "glGetProgramiv");
        GetProgramInfoLog = LoadProc<GlGetProgramInfoLog>(getProcAddress, "glGetProgramInfoLog");
        DeleteShader = LoadProc<GlDeleteShader>(getProcAddress, "glDeleteShader");
        DeleteProgram = LoadProc<GlDeleteProgram>(getProcAddress, "glDeleteProgram");
        UseProgram = LoadProc<GlUseProgram>(getProcAddress, "glUseProgram");
        GenVertexArrays = LoadProc<GlGenVertexArrays>(getProcAddress, "glGenVertexArrays");
        DeleteVertexArrays = LoadProc<GlDeleteVertexArrays>(getProcAddress, "glDeleteVertexArrays");
        BindVertexArray = LoadProc<GlBindVertexArray>(getProcAddress, "glBindVertexArray");
        GetUniformLocation = LoadProc<GlGetUniformLocation>(getProcAddress, "glGetUniformLocation");
        Uniform1f = LoadProc<GlUniform1f>(getProcAddress, "glUniform1f");
        Uniform1i = LoadProc<GlUniform1i>(getProcAddress, "glUniform1i");
        Uniform2f = LoadProc<GlUniform2f>(getProcAddress, "glUniform2f");
        Uniform2i = LoadProc<GlUniform2i>(getProcAddress, "glUniform2i");
        Uniform3f = LoadProc<GlUniform3f>(getProcAddress, "glUniform3f");
        GenTextures = LoadProc<GlGenTextures>(getProcAddress, "glGenTextures");
        BindTexture = LoadProc<GlBindTexture>(getProcAddress, "glBindTexture");
        ActiveTexture = LoadProc<GlActiveTexture>(getProcAddress, "glActiveTexture");
        TexParameteri = LoadProc<GlTexParameteri>(getProcAddress, "glTexParameteri");
        PixelStorei = LoadProc<GlPixelStorei>(getProcAddress, "glPixelStorei");
        TexImage2D = LoadProc<GlTexImage2D>(getProcAddress, "glTexImage2D");
        GenFramebuffers = LoadProc<GlGenFramebuffers>(getProcAddress, "glGenFramebuffers");
        BindFramebuffer = LoadProc<GlBindFramebuffer>(getProcAddress, "glBindFramebuffer");
        FramebufferTexture2D = LoadProc<GlFramebufferTexture2D>(getProcAddress, "glFramebufferTexture2D");
        CheckFramebufferStatus = LoadProc<GlCheckFramebufferStatus>(getProcAddress, "glCheckFramebufferStatus");
        DeleteFramebuffers = LoadProc<GlDeleteFramebuffers>(getProcAddress, "glDeleteFramebuffers");
        DeleteTextures = LoadProc<GlDeleteTextures>(getProcAddress, "glDeleteTextures");
        Viewport = LoadProc<GlViewport>(getProcAddress, "glViewport");
        DrawArrays = LoadProc<GlDrawArrays>(getProcAddress, "glDrawArrays");
        ReadPixels = LoadProc<GlReadPixels>(getProcAddress, "glReadPixels");
        Finish = LoadProc<GlFinish>(getProcAddress, "glFinish");
        GetString = LoadProc<GlGetString>(getProcAddress, "glGetString");
        GetIntegerv = LoadProc<GlGetIntegerv>(getProcAddress, "glGetIntegerv");
        ClearColor = LoadProc<GlClearColor>(getProcAddress, "glClearColor");
        Clear = LoadProc<GlClear>(getProcAddress, "glClear");
        Enable = LoadProc<GlEnable>(getProcAddress, "glEnable");
        Disable = LoadProc<GlDisable>(getProcAddress, "glDisable");
        BlendFunc = LoadProc<GlBlendFunc>(getProcAddress, "glBlendFunc");
        BlendFuncSeparate = LoadProc<GlBlendFuncSeparate>(getProcAddress, "glBlendFuncSeparate");
    }

    private static T LoadProc<T>(Func<string, IntPtr> getProcAddress, string name) where T : Delegate
    {
        var ptr = getProcAddress(name);
        if (ptr == IntPtr.Zero) throw new EntryPointNotFoundException(name);
        return Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }

    public static unsafe uint CompileProgram(string vertex, string fragment)
    {
        uint vs = Compile(GL_VERTEX_SHADER, vertex);
        uint fs = Compile(GL_FRAGMENT_SHADER, fragment);
        uint p = CreateProgram();
        AttachShader(p, vs); AttachShader(p, fs); LinkProgram(p);
        GetProgramiv(p, GL_LINK_STATUS, out var ok);
        if (ok == 0)
        {
            GetProgramiv(p, GL_INFO_LOG_LENGTH, out var len);
            var buf = new byte[Math.Max(1, len)];
            fixed (byte* ptr = buf) GetProgramInfoLog(p, buf.Length, out _, ptr);
            DeleteShader(vs); DeleteShader(fs); DeleteProgram(p);
            throw new InvalidOperationException("OpenGL program link failed: " + Encoding.UTF8.GetString(buf).TrimEnd('\0'));
        }
        DeleteShader(vs); DeleteShader(fs);
        return p;
    }

    private static unsafe uint Compile(uint type, string source)
    {
        uint shader = CreateShader(type);
        byte[] bytes = Encoding.UTF8.GetBytes(source);
        fixed (byte* p = bytes)
        {
            byte* q = p;
            int len = bytes.Length;
            ShaderSource(shader, 1, &q, &len);
        }
        CompileShader(shader);
        GetShaderiv(shader, GL_COMPILE_STATUS, out var ok);
        if (ok == 0)
        {
            GetShaderiv(shader, GL_INFO_LOG_LENGTH, out var len);
            var buf = new byte[Math.Max(1, len)];
            fixed (byte* ptr = buf) GetShaderInfoLog(shader, buf.Length, out _, ptr);
            DeleteShader(shader);
            throw new InvalidOperationException("OpenGL shader compile failed: " + Encoding.UTF8.GetString(buf).TrimEnd('\0'));
        }
        return shader;
    }
}
