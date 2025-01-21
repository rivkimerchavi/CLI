using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

var rootCommand = new RootCommand("Root command for file Bundler CLI");


var bundleCommand = new Command("bundle", "Bundle code files into a single file");
rootCommand.AddCommand(bundleCommand);
var languageOption = new Option<string>("--language", "Comma-separated list of programming languages or 'all'") { IsRequired = true };
bundleCommand.AddOption(languageOption);
languageOption.AddAlias("-l");
var outputOption = new Option<FileInfo>("--output", "Output file path and name") { IsRequired = true };
bundleCommand.AddOption(outputOption);
outputOption.AddAlias("-o");

var noteOption = new Option<bool>("--note", "Include source file paths as comments") { };
bundleCommand.AddOption(noteOption);
noteOption.AddAlias("-n");

var sortOption = new Option<string>("--sort", "Sort files by name or type") { Arity = ArgumentArity.ZeroOrOne };
bundleCommand.AddOption(sortOption);
sortOption.AddAlias("-s");

var removeEmptyLinesOption = new Option<bool>("--remove-empty-lines", "Remove empty lines from code");
bundleCommand.AddOption(removeEmptyLinesOption);
removeEmptyLinesOption.AddAlias("-r");

var authorOption = new Option<string>("--author", "Author name for the bundle comment") { Arity = ArgumentArity.ZeroOrOne };
bundleCommand.AddOption(authorOption);
authorOption.AddAlias("-a");

bundleCommand.SetHandler((language, output, note, sort, removeEmptyLines, author) =>
{
    try
    {
        var languages = language.Split(',').Select(l => l.Trim()).ToList();
        CreateBundle(languages, output.FullName, note, sort, removeEmptyLines, author);
    }
    catch (DirectoryNotFoundException)
    {
        Console.WriteLine("Directory not found for the specified output file.");
    }
    catch (IOException e)
    {
        Console.WriteLine($"I/O error: {e.Message}");
    }
    catch (UnauthorizedAccessException e)
    {
        Console.WriteLine($"Access error: {e.Message}");
    }
}, languageOption, outputOption, noteOption, sortOption, removeEmptyLinesOption, authorOption);

static List<string> GetSupportedLanguages()
{
    return new List<string> { "csharp", "java", "python", "javascript", "cpp" };
}

static void CreateBundle(List<string> languages, string output, bool note, string sortBy, bool removeEmptyLines, string author)
{
    var supportedLanguages = GetSupportedLanguages();

    foreach (var language in languages)
    {
        if (language != "all" && !supportedLanguages.Contains(language.ToLower()))
        {
            Console.WriteLine($"The language '{language}' is not supported. Supported languages are: {string.Join(", ", supportedLanguages)}.");
            return;
        }
    }


    var excludedDirectories = new[] { "bin", "debug" };
    var searchOption = SearchOption.AllDirectories;

    var codeFiles = languages.Contains("all")
        ? Directory.GetFiles(Directory.GetCurrentDirectory(), "*.*", searchOption)
                    .Where(file => !excludedDirectories.Any(dir => file.Contains(Path.Combine(Directory.GetCurrentDirectory(), dir))))
        : languages.SelectMany(lang => Directory.GetFiles(Directory.GetCurrentDirectory(), $"*.{lang}", searchOption)
                    .Where(file => !excludedDirectories.Any(dir => file.Contains(Path.Combine(Directory.GetCurrentDirectory(), dir)))));

    if (!codeFiles.Any())
    {
        Console.WriteLine("No code files found for the specified languages. Creating an empty output file.");
        File.WriteAllText(output, string.Empty); 
        return;
    }

    if (!string.IsNullOrEmpty(sortBy) && sortBy != "name" && sortBy != "type")
    {
        Console.WriteLine("Invalid sort option. Use 'name' or 'type'.");
        return;
    }

    codeFiles = sortBy == "type"
        ? codeFiles.OrderBy(f => Path.GetExtension(f)).ToArray()
        : codeFiles.OrderBy(f => Path.GetFileName(f)).ToArray();

    try
    {
        var bundleContent = new StringBuilder();

        if (!string.IsNullOrEmpty(author))
        {
            bundleContent.AppendLine($"// Author: {author}");
        }

        foreach (var file in codeFiles)
        {
            var lines = File.ReadAllLines(file);

            if (removeEmptyLines)
            {
                lines = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            }

            if (note)
            {
                bundleContent.AppendLine($"// Source: {file}");
            }

            foreach (var line in lines)
            {
                bundleContent.AppendLine(line);
            }
        }

        File.WriteAllText(output, bundleContent.ToString());
    }
    catch (Exception e)
    {
        Console.WriteLine($"Error saving bundle: {e.Message}");
    }
}


var createRspCommand = new Command("create-rsp", "Create a response file for the bundle command");
rootCommand.AddCommand(createRspCommand);

createRspCommand.SetHandler(() =>
{
    string output, language, sort, author;
    bool note, removeEmptyLines;

    Console.Write("Enter output file path: ");
    output = Console.ReadLine();

    Console.Write("Enter programming languages (comma-separated or 'all'): ");
    language = Console.ReadLine();

    Console.Write("Include source file paths as comments (true/false): ");
    note = bool.Parse(Console.ReadLine());

    Console.Write("Sort files by name or type (name/type): ");
    sort = Console.ReadLine();

    Console.Write("Remove empty lines from code (true/false): ");
    removeEmptyLines = bool.Parse(Console.ReadLine());

    Console.Write("Enter author name (optional): ");
    author = Console.ReadLine();


    var commandLine = $"bundle --output \"{output}\" --language \"{language}\" --note {note.ToString().ToLower()} --sort \"{sort}\" --remove-empty-lines {removeEmptyLines.ToString().ToLower()}";

   
    File.WriteAllText($"{Path.GetFileNameWithoutExtension(output)}.rsp", commandLine);
    Console.WriteLine($"Response file created: {Path.GetFileNameWithoutExtension(output)}.rsp");
});


rootCommand.InvokeAsync(args);
