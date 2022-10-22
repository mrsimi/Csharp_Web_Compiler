using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CSharp;
using Microsoft.Extensions.Logging;

namespace csharp_pad.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;

    [BindProperty]
    public string OutputResult {get; set;}

    [BindProperty]
    public string ContentCode {get; set;}


    public IndexModel(ILogger<IndexModel> logger)
    {
        _logger = logger;
    }

    public IActionResult OnGet(string? output)
    {
        OutputResult = output;
        Console.WriteLine(OutputResult);
        ViewData["Result"] = output;
        return Page();
    }

    public JsonResult OnPost(string writtenCode)
    {
       string debugOutput = string.Empty;
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(writtenCode);

        string assemblyName = Path.GetRandomFileName();

        var refPaths = new[] {
                typeof(System.Object).GetTypeInfo().Assembly.Location,
                typeof(Console).GetTypeInfo().Assembly.Location,
                Path.Combine(Path.GetDirectoryName(typeof(System.Runtime.GCSettings)
                .GetTypeInfo().Assembly.Location), "System.Runtime.dll")
        };

        MetadataReference[] references = refPaths.Select(r => MetadataReference.CreateFromFile(r)).ToArray();

        CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using (var ms = new MemoryStream())
        {
            EmitResult result = compilation.Emit(ms);

            if (!result.Success)
            {
                IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                    diagnostic.IsWarningAsError ||
                    diagnostic.Severity == DiagnosticSeverity.Error);

                foreach (Diagnostic diagnostic in failures)
                {
                    debugOutput = debugOutput + "\n"+ diagnostic.Id.ToString() +" " +diagnostic.GetMessage();
                }
            }
            else
            {
                ms.Seek(0, SeekOrigin.Begin);

                Assembly assembly = AssemblyLoadContext.Default.LoadFromStream(ms);
                var type = assembly.GetType("RoslynCompileSample.Writer");
                var instance = assembly.CreateInstance("RoslynCompileSample.Writer");
                var meth = type.GetMember("Main").First() as MethodInfo;
                var methodResult = meth.Invoke(instance, new[] { new string[] { "road", "air" } });
                
                debugOutput = methodResult.ToString();

               // Console.WriteLine(OutputResult);
            }

            //return OnGet(OutputResult);
            return new JsonResult(debugOutput);
        }

    }
}
