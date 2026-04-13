---
description: Comprehensive dead code audit — finds all unused files, functions, classes, and variables by walking the call graph from actual entry points. Run periodically (after several features merge, or before a release), not per-PR. Produces DEADCODE.md.
reference: https://www.josecasanova.com/prompts/dead-code-audit
---

You will be conducting a comprehensive audit of a codebase to identify ALL dead code that can be safely deleted. This requires extremely thorough analysis from multiple angles to ensure nothing is missed.

Your task is to identify ALL files, functions, methods, classes, variables, and any other code elements that are no longer being used anywhere in the codebase and can be deleted. You must be AGGRESSIVE in this identification while being absolutely certain the code is truly unused.

## Analysis Methodology

You must approach this audit from multiple perspectives to ensure completeness:

1. **Static Analysis Perspective**: Trace all imports, function calls, class instantiations, and variable references
2. **Entry Point Analysis**: Start from main entry points (main functions, API endpoints, CLI commands) and trace all reachable code
3. **Configuration Analysis**: Check configuration files, build scripts, and deployment files for referenced code
4. **Test Code Analysis**: Examine test files to identify code that may only be used in testing
5. **Dynamic/Runtime Analysis**: Look for code that might be called dynamically (reflection, string-based calls, etc.)
6. **Documentation Analysis**: Check if code is referenced in comments or documentation as examples

## Dead Code Criteria

Code should be considered dead if it meets ALL of these conditions:

- Not called/imported/referenced anywhere in the codebase
- Not an entry point (main functions, API endpoints, CLI commands)
- Not used by external systems or APIs
- Not required for future planned functionality (be conservative here)
- Not part of a public API that external consumers might use
- Not dynamically invoked through reflection or string-based calls

## Verification Process

For each potential dead code item:

1. Search for ALL possible references (exact name matches, partial matches, string references)
2. Check if it's part of an inheritance hierarchy that's used elsewhere
3. Verify it's not a callback or event handler
4. Confirm it's not referenced in configuration files or external scripts
5. Double-check it's not used in any form of dynamic dispatch

## Analysis Process

<scratchpad>
Use this space to conduct your thorough analysis:

1. First, map out the entire codebase structure and identify all entry points
2. Create a comprehensive list of all code elements (files, classes, functions, methods, variables)
3. For each element, systematically trace all references and usage
4. Apply each of the analysis perspectives listed above
5. Cross-reference findings to ensure consistency
6. Identify any edge cases or uncertain situations
7. Compile your final list of confirmed dead code

Be extremely thorough in this analysis. Consider multiple scenarios and edge cases. If you have any doubt about whether code is used, err on the side of caution and do not mark it as dead code.
</scratchpad>

After completing your analysis, create a comprehensive DEADCODE.md file with your findings.

## Output Requirements

Your final response should contain ONLY the complete DEADCODE.md file content. The file should be structured as follows:

# Dead Code Audit Report

## Summary

[Brief summary of findings - total files, functions, classes, etc. identified for deletion]

## Files to Delete

[List each file with brief explanation of why it's unused]

## Functions/Methods to Delete

[Organized by file, list each function/method with explanation]

## Classes to Delete

[List each class with explanation]

## Variables/Constants to Delete

[List unused variables and constants]

## Verification Notes

[Any important notes about verification process or edge cases considered]

## Estimated Impact

[Rough estimate of lines of code that can be removed]

Be extremely thorough and aggressive in identifying dead code, but only include items you are absolutely certain are unused. Your analysis must be comprehensive enough that a developer could confidently delete everything listed without breaking the system.
