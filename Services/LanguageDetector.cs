using System.Text.RegularExpressions;

namespace Kapture.Services;

public static partial class LanguageDetector
{
    private static readonly (string LanguageId, string DisplayName, Func<string, int> Score)[] Languages =
    [
        ("json", "JSON", ScoreJson),
        ("xml", "XML", ScoreXml),
        ("html", "HTML", ScoreHtml),
        ("css", "CSS", ScoreCss),
        ("sql", "SQL", ScoreSql),
        ("yaml", "YAML", ScoreYaml),
        ("markdown", "Markdown", ScoreMarkdown),
        ("csharp", "C#", ScoreCSharp),
        ("typescript", "TypeScript", ScoreTypeScript),
        ("javascript", "JavaScript", ScoreJavaScript),
        ("python", "Python", ScorePython),
        ("java", "Java", ScoreJava),
        ("go", "Go", ScoreGo),
        ("rust", "Rust", ScoreRust),
        ("cpp", "C++", ScoreCpp),
        ("c", "C", ScoreC),
        ("php", "PHP", ScorePhp),
        ("ruby", "Ruby", ScoreRuby),
        ("swift", "Swift", ScoreSwift),
        ("kotlin", "Kotlin", ScoreKotlin),
        ("shellscript", "Shell", ScoreShell),
        ("powershell", "PowerShell", ScorePowerShell),
    ];

    private static readonly Dictionary<string, string> DisplayNames =
        Languages.ToDictionary(l => l.LanguageId, l => l.DisplayName);

    public static string? Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 10) return null;

        string? best = null;
        int bestScore = 2; // Minimum threshold to avoid false positives

        foreach (var (langId, _, scorer) in Languages)
        {
            var score = scorer(text);
            if (score > bestScore)
            {
                bestScore = score;
                best = langId;
            }
        }

        return best;
    }

    public static string GetDisplayName(string languageId)
        => DisplayNames.TryGetValue(languageId, out var name) ? name : languageId;

    // --- Scoring functions ---
    // Each returns a score 0+; higher = more confident

    private static int ScoreJson(string t)
    {
        var s = 0;
        var trimmed = t.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('[')) s += 2;
        s += CountMatches(t, JsonKeyRegex());
        if (t.Contains("\":\n") || t.Contains("\": ")) s++;
        return s;
    }

    private static int ScoreXml(string t)
    {
        var s = 0;
        if (t.Contains("<?xml")) s += 5;
        s += CountMatches(t, XmlTagRegex());
        if (t.Contains("</") && t.Contains("/>")) s += 2;
        return s;
    }

    private static int ScoreHtml(string t)
    {
        var s = 0;
        var lower = t.ToLowerInvariant();
        if (lower.Contains("<html") || lower.Contains("<!doctype html")) s += 5;
        foreach (var tag in new[] { "<div", "<span", "<head", "<body", "<script", "<style", "<meta", "<link", "<img", "<form" })
            if (lower.Contains(tag)) s += 2;
        return s;
    }

    private static int ScoreCss(string t)
    {
        var s = 0;
        s += CountMatches(t, CssSelectorRegex()) * 2;
        foreach (var prop in new[] { "color:", "margin:", "padding:", "display:", "font-size:", "background:", "border:", "width:", "height:" })
            if (t.Contains(prop)) s++;
        if (t.Contains("@media") || t.Contains("@import") || t.Contains("@keyframes")) s += 3;
        return s;
    }

    private static int ScoreSql(string t)
    {
        var s = 0;
        var upper = t.ToUpperInvariant();
        foreach (var kw in new[] { "SELECT ", "INSERT ", "UPDATE ", "DELETE ", "CREATE TABLE", "ALTER TABLE", "DROP TABLE",
                                    "FROM ", "WHERE ", "JOIN ", "GROUP BY", "ORDER BY", "HAVING ", "INNER JOIN", "LEFT JOIN" })
            if (upper.Contains(kw)) s += 2;
        return s;
    }

    private static int ScoreYaml(string t)
    {
        var s = 0;
        if (t.StartsWith("---")) s += 3;
        s += CountMatches(t, YamlKeyRegex());
        if (t.Contains("  - ") || t.Contains("- name:")) s += 2;
        return s;
    }

    private static int ScoreMarkdown(string t)
    {
        var s = 0;
        s += CountMatches(t, MdHeadingRegex()) * 2;
        if (t.Contains("```")) s += 3;
        s += CountMatches(t, MdLinkRegex());
        if (t.Contains("- ") || t.Contains("* ")) s++;
        return s;
    }

    private static int ScoreCSharp(string t)
    {
        var s = 0;
        if (t.Contains("using System")) s += 3;
        if (t.Contains("namespace ")) s += 2;
        foreach (var kw in new[] { "public class ", "private ", "protected ", "internal ", "async Task", "=> ", "var ", ".ToString()", "string ", "List<", "IEnumerable<" })
            if (t.Contains(kw)) s++;
        if (CSharpAttrRegex().IsMatch(t)) s += 2;
        return s;
    }

    private static int ScoreTypeScript(string t)
    {
        var s = ScoreJavaScript(t);
        if (t.Contains(": string") || t.Contains(": number") || t.Contains(": boolean")) s += 3;
        if (t.Contains("interface ") || t.Contains("<T>") || t.Contains("as ")) s += 2;
        if (t.Contains("import type")) s += 3;
        return s;
    }

    private static int ScoreJavaScript(string t)
    {
        var s = 0;
        foreach (var kw in new[] { "const ", "let ", "var ", "function ", "=> ", "require(", "import ", "export ", "module.exports",
                                    "console.log", "document.", "window.", "async ", "await ", "Promise" })
            if (t.Contains(kw)) s++;
        if (t.Contains("===") || t.Contains("!==")) s += 2;
        return s;
    }

    private static int ScorePython(string t)
    {
        var s = 0;
        foreach (var kw in new[] { "def ", "class ", "import ", "from ", "self.", "print(", "__init__", "elif ", "True", "False", "None" })
            if (t.Contains(kw)) s++;
        if (PythonDefRegex().IsMatch(t)) s += 2;
        if (t.Contains("    ") && !t.Contains("{")) s++;  // indentation-based
        if (t.StartsWith("#!/usr/bin/env python") || t.StartsWith("#!/usr/bin/python")) s += 5;
        return s;
    }

    private static int ScoreJava(string t)
    {
        var s = 0;
        foreach (var kw in new[] { "public static void main", "System.out.println", "import java.", "extends ", "implements ",
                                    "@Override", "new ArrayList", "throws ", "private final " })
            if (t.Contains(kw)) s += 2;
        if (t.Contains("package ") && t.Contains(";")) s += 2;
        return s;
    }

    private static int ScoreGo(string t)
    {
        var s = 0;
        if (t.Contains("package main")) s += 4;
        foreach (var kw in new[] { "func ", "fmt.", "import (", ":= ", "go func", "chan ", "defer ", "goroutine", "interface{}" })
            if (t.Contains(kw)) s += 2;
        if (t.Contains("if err != nil")) s += 3;
        return s;
    }

    private static int ScoreRust(string t)
    {
        var s = 0;
        foreach (var kw in new[] { "fn ", "let mut ", "impl ", "pub fn", "use std::", "match ", "Some(", "None", "Ok(", "Err(",
                                    "println!(", "vec![", "&self", "-> ", "unwrap()", "#[derive" })
            if (t.Contains(kw)) s += 2;
        return s;
    }

    private static int ScoreCpp(string t)
    {
        var s = ScoreC(t);
        foreach (var kw in new[] { "std::", "cout", "cin", "endl", "nullptr", "class ", "template<", "namespace ", "vector<", "string ", "auto " })
            if (t.Contains(kw)) s += 2;
        if (t.Contains("#include <iostream>") || t.Contains("#include <vector>")) s += 3;
        return s;
    }

    private static int ScoreC(string t)
    {
        var s = 0;
        foreach (var kw in new[] { "#include ", "#define ", "printf(", "malloc(", "sizeof(", "int main(", "void ", "NULL", "typedef ", "struct " })
            if (t.Contains(kw)) s++;
        if (t.Contains("#include <stdio.h>") || t.Contains("#include <stdlib.h>")) s += 3;
        return s;
    }

    private static int ScorePhp(string t)
    {
        var s = 0;
        if (t.Contains("<?php")) s += 5;
        foreach (var kw in new[] { "$this->", "echo ", "function ", "->", "$_GET", "$_POST", "$_SESSION", "use ", "namespace " })
            if (t.Contains(kw)) s++;
        if (PhpVarRegex().IsMatch(t)) s += 2;
        return s;
    }

    private static int ScoreRuby(string t)
    {
        var s = 0;
        foreach (var kw in new[] { "def ", "end\n", "puts ", "require ", "attr_accessor", "class ", "do |", ".each ", "nil", "elsif" })
            if (t.Contains(kw)) s++;
        if (t.StartsWith("#!/usr/bin/env ruby") || t.StartsWith("#!/usr/bin/ruby")) s += 5;
        if (RubySymbolRegex().IsMatch(t)) s += 2;
        return s;
    }

    private static int ScoreSwift(string t)
    {
        var s = 0;
        foreach (var kw in new[] { "import Foundation", "import UIKit", "import SwiftUI", "func ", "var ", "let ", "guard ",
                                    "struct ", "protocol ", "extension ", "optional", "?.unwrap" })
            if (t.Contains(kw)) s++;
        if (t.Contains("-> ") && t.Contains("func ")) s += 2;
        return s;
    }

    private static int ScoreKotlin(string t)
    {
        var s = 0;
        foreach (var kw in new[] { "fun ", "val ", "var ", "println(", "data class", "companion object", "suspend fun",
                                    "import kotlin", "override fun", "when ", "is ", "sealed class" })
            if (t.Contains(kw)) s++;
        if (t.Contains("?.") && t.Contains("fun ")) s += 2;
        return s;
    }

    private static int ScoreShell(string t)
    {
        var s = 0;
        if (t.StartsWith("#!/bin/bash") || t.StartsWith("#!/bin/sh") || t.StartsWith("#!/usr/bin/env bash")) s += 5;
        foreach (var kw in new[] { "echo ", "if [", "fi\n", "then\n", "done\n", "esac", "export ", "chmod ", "grep ", "awk ", "sed " })
            if (t.Contains(kw)) s++;
        if (ShellVarRegex().IsMatch(t)) s += 2;
        return s;
    }

    private static int ScorePowerShell(string t)
    {
        var s = 0;
        foreach (var kw in new[] { "Write-Host", "Get-", "Set-", "New-", "Import-Module", "param(", "$PSVersionTable",
                                    "ForEach-Object", "-eq ", "-ne ", "| Select-Object", "[CmdletBinding()]" })
            if (t.Contains(kw)) s += 2;
        if (PsVarRegex().IsMatch(t)) s += 2;
        return s;
    }

    private static int CountMatches(string text, Regex regex)
        => regex.Matches(text).Count;

    // Generated regex patterns
    [GeneratedRegex(@"""[\w]+"":\s")]
    private static partial Regex JsonKeyRegex();

    [GeneratedRegex(@"<\w+[\s>]")]
    private static partial Regex XmlTagRegex();

    [GeneratedRegex(@"[\w.#\[\]=]+\s*\{")]
    private static partial Regex CssSelectorRegex();

    [GeneratedRegex(@"^[\w_]+:", RegexOptions.Multiline)]
    private static partial Regex YamlKeyRegex();

    [GeneratedRegex(@"^#{1,6}\s", RegexOptions.Multiline)]
    private static partial Regex MdHeadingRegex();

    [GeneratedRegex(@"\[.+\]\(.+\)")]
    private static partial Regex MdLinkRegex();

    [GeneratedRegex(@"\[\w+\]")]
    private static partial Regex CSharpAttrRegex();

    [GeneratedRegex(@"def\s+\w+\s*\(")]
    private static partial Regex PythonDefRegex();

    [GeneratedRegex(@"\$\w+")]
    private static partial Regex PhpVarRegex();

    [GeneratedRegex(@":\w+")]
    private static partial Regex RubySymbolRegex();

    [GeneratedRegex(@"\$\w+")]
    private static partial Regex PsVarRegex();

    [GeneratedRegex(@"\$\{\w+\}")]
    private static partial Regex ShellVarRegex();
}
