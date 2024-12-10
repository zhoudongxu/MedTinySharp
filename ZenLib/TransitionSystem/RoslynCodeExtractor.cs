using System;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class RoslynCodeExtractor
{
    // 从源代码文件中提取方法的源代码
    public static string GetMethodCodeFromSourceFile(string fileName, string methodName)
    {
        // 获取当前执行目录
        string currentDir = Directory.GetCurrentDirectory();

        // 假设源代码文件相对于当前工作目录
        string filePath = Path.Combine(currentDir, fileName);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File {filePath} not found.");
        }

        // 解析源代码文件
        return GetMethodCodeFromSourceFile(filePath, methodName);
    }

    // 通过 Roslyn 获取源代码
    public static string GetMethodCodeFromSourceFile(string filePath, string methodName)
    {
        var code = File.ReadAllText(filePath);

        // 使用 Roslyn 解析源代码
        var syntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(code);
        var root = syntaxTree.GetRoot() as Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax;

        // 查找方法
        var methodNode = root.DescendantNodes()
                              .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
                              .FirstOrDefault(m => m.Identifier.Text == methodName);

        if (methodNode != null)
        {
            // 返回方法的源代码
            return methodNode.NormalizeWhitespace().ToFullString();
        }

        return null;
    }
}
