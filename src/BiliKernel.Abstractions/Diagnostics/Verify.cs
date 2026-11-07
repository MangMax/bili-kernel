// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Richasy.BiliKernel;

[ExcludeFromCodeCoverage]
internal static partial class Verify
{
    [GeneratedRegex("^[^.]+\\.[^.]+$")]
    private static partial Regex FilenameRegex();

    /// <summary>
    /// Equivalent of ArgumentNullException.ThrowIfNull
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void NotNull([NotNull] object? obj, [CallerArgumentExpression(nameof(obj))] string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(obj, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void NotNullOrWhiteSpace([NotNull] string? str, [CallerArgumentExpression(nameof(str))] string? paramName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(str, paramName);
    }

    internal static void NotNullOrEmpty<T>(IList<T> list, [CallerArgumentExpression(nameof(list))] string? paramName = null)
    {
        NotNull(list, paramName);
        if (list.Count == 0)
        {
            throw new ArgumentException("The value cannot be empty.", paramName);
        }
    }

    public static void True(bool condition, string message, [CallerArgumentExpression(nameof(condition))] string? paramName = null)
    {
        if (!condition)
        {
            throw new ArgumentException(message, paramName);
        }
    }

    internal static void ValidFilename([NotNull] string? filename, [CallerArgumentExpression(nameof(filename))] string? paramName = null)
    {
        NotNullOrWhiteSpace(filename);
        if (!FilenameRegex().IsMatch(filename))
        {
            throw new ArgumentException($"Invalid filename format: '{filename}'. Filename should consist of an actual name and a file extension.", paramName);
        }
    }

    public static void ValidateUrl(string url, bool allowQuery = false, [CallerArgumentExpression(nameof(url))] string? paramName = null)
    {
        NotNullOrWhiteSpace(url, paramName);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
        {
            throw new ArgumentException($"The `{url}` is not valid.", paramName);
        }

        if (!allowQuery && !string.IsNullOrEmpty(uri.Query))
        {
            throw new ArgumentException($"The `{url}` is not valid: it cannot contain query parameters.", paramName);
        }

        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException($"The `{url}` is not valid: it cannot contain URL fragments.", paramName);
        }
    }

    internal static void StartsWith([NotNull] string? text, string prefix, string message, [CallerArgumentExpression(nameof(text))] string? textParamName = null)
    {
        Debug.Assert(prefix is not null);

        NotNullOrWhiteSpace(text, textParamName);
        if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(textParamName, message);
        }
    }

    internal static void DirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Directory '{path}' could not be found.");
        }
    }
}
