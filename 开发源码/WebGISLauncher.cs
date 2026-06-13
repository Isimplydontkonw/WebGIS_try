using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

internal static class WebGISLauncher
{
    private const int Port = 8765;
    private static readonly string Root =
        Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

    private static readonly Dictionary<string, string> MimeTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".html", "text/html; charset=utf-8" },
            { ".css", "text/css; charset=utf-8" },
            { ".js", "application/javascript; charset=utf-8" },
            { ".json", "application/json; charset=utf-8" },
            { ".geojson", "application/geo+json; charset=utf-8" },
            { ".png", "image/png" },
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".gif", "image/gif" },
            { ".svg", "image/svg+xml" },
            { ".ico", "image/x-icon" }
        };

    private static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "WebGIS 地图服务";

        if (!File.Exists(Path.Combine(Root, "index.html")))
        {
            Fail("未找到 index.html。\n\n请将 WebGIS地图.exe 放在项目文件夹中运行。");
            return;
        }

        TcpListener listener = new TcpListener(IPAddress.Loopback, Port);
        try
        {
            listener.Start();
        }
        catch (SocketException)
        {
            OpenBrowser();
            Fail("地图服务可能已经启动。\n\n已尝试重新打开浏览器。");
            return;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("WebGIS 地图已启动");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("浏览器地址：http://localhost:{0}/", Port);
        Console.WriteLine("请保持此窗口打开，关闭窗口即可停止地图。");
        Console.WriteLine();

        OpenBrowser();

        try
        {
            while (true)
            {
                using (TcpClient client = listener.AcceptTcpClient())
                {
                    Serve(client);
                }
            }
        }
        catch (Exception ex)
        {
            Fail("地图服务发生错误：\n\n" + ex.Message);
        }
        finally
        {
            listener.Stop();
        }
    }

    private static void Serve(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        stream.ReadTimeout = 5000;

        string requestLine;
        using (StreamReader reader = new StreamReader(
            stream, Encoding.ASCII, false, 1024, true))
        {
            requestLine = reader.ReadLine();
            string line;
            while (!string.IsNullOrEmpty(line = reader.ReadLine()))
            {
            }
        }

        if (string.IsNullOrWhiteSpace(requestLine))
        {
            return;
        }

        string[] requestParts = requestLine.Split(' ');
        if (requestParts.Length < 2)
        {
            WriteResponse(stream, "400 Bad Request", "text/plain", new byte[0], false);
            return;
        }

        bool headOnly = string.Equals(requestParts[0], "HEAD", StringComparison.OrdinalIgnoreCase);
        string target = requestParts[1];
        int queryIndex = target.IndexOf('?');
        if (queryIndex >= 0)
        {
            target = target.Substring(0, queryIndex);
        }

        string requestPath = Uri.UnescapeDataString(target);
        if (requestPath == "/")
        {
            requestPath = "/index.html";
        }

        string relativePath = requestPath.TrimStart('/')
            .Replace('/', Path.DirectorySeparatorChar);
        string requestedFile = Path.GetFullPath(Path.Combine(Root, relativePath));
        string rootPrefix = Path.GetFullPath(Root)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!requestedFile.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            WriteResponse(stream, "403 Forbidden", "text/plain", new byte[0], headOnly);
            return;
        }

        if (!File.Exists(requestedFile))
        {
            WriteResponse(stream, "404 Not Found", "text/plain", new byte[0], headOnly);
            return;
        }

        byte[] content = File.ReadAllBytes(requestedFile);
        string extension = Path.GetExtension(requestedFile);
        string contentType;
        if (!MimeTypes.TryGetValue(extension, out contentType))
        {
            contentType = "application/octet-stream";
        }

        WriteResponse(stream, "200 OK", contentType, content, headOnly);
    }

    private static void WriteResponse(
        NetworkStream stream,
        string status,
        string contentType,
        byte[] content,
        bool headOnly)
    {
        string header =
            "HTTP/1.1 " + status + "\r\n" +
            "Content-Type: " + contentType + "\r\n" +
            "Content-Length: " + content.Length + "\r\n" +
            "Cache-Control: no-cache\r\n" +
            "Connection: close\r\n\r\n";

        byte[] headerBytes = Encoding.ASCII.GetBytes(header);
        stream.Write(headerBytes, 0, headerBytes.Length);
        if (!headOnly && content.Length > 0)
        {
            stream.Write(content, 0, content.Length);
        }
    }

    private static void OpenBrowser()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "http://localhost:" + Port + "/index.html",
            UseShellExecute = true
        });
    }

    private static void Fail(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("按任意键关闭...");
        Console.ReadKey(true);
    }
}
