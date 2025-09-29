// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// Suppress all nullable reference warnings globally
[assembly: SuppressMessage("Microsoft.Design", "CS8600:Converting null literal or possible null value to non-nullable type", Justification = "Nullable warnings suppressed for MAUI compatibility", Scope = "namespaceanddescendants", Target = "~N:MapLocationApp")]
[assembly: SuppressMessage("Microsoft.Design", "CS8601:Possible null reference assignment", Justification = "Nullable warnings suppressed for MAUI compatibility", Scope = "namespaceanddescendants", Target = "~N:MapLocationApp")]
[assembly: SuppressMessage("Microsoft.Design", "CS8602:Dereference of a possibly null reference", Justification = "Nullable warnings suppressed for MAUI compatibility", Scope = "namespaceanddescendants", Target = "~N:MapLocationApp")]
[assembly: SuppressMessage("Microsoft.Design", "CS8603:Possible null reference return", Justification = "Nullable warnings suppressed for MAUI compatibility", Scope = "namespaceanddescendants", Target = "~N:MapLocationApp")]
[assembly: SuppressMessage("Microsoft.Design", "CS8604:Possible null reference argument", Justification = "Nullable warnings suppressed for MAUI compatibility", Scope = "namespaceanddescendants", Target = "~N:MapLocationApp")]
[assembly: SuppressMessage("Microsoft.Design", "CS8618:Non-nullable field must contain a non-null value when exiting constructor", Justification = "Fields are initialized through dependency injection or other means", Scope = "namespaceanddescendants", Target = "~N:MapLocationApp")]
[assembly: SuppressMessage("Microsoft.Design", "CS8619:Nullability of reference types in value doesn't match target type", Justification = "Nullable warnings suppressed for MAUI compatibility", Scope = "namespaceanddescendants", Target = "~N:MapLocationApp")]
[assembly: SuppressMessage("Microsoft.Design", "CS8622:Nullability of reference types in type of parameter doesn't match the target delegate", Justification = "Event handlers commonly accept nullable sender parameters", Scope = "namespaceanddescendants", Target = "~N:MapLocationApp")]
[assembly: SuppressMessage("Microsoft.Design", "CS8625:Cannot convert null literal to non-nullable reference type", Justification = "Nullable warnings suppressed for MAUI compatibility", Scope = "namespaceanddescendants", Target = "~N:MapLocationApp")]
[assembly: SuppressMessage("Microsoft.Design", "CS8629:Nullable value type may be null", Justification = "Nullable warnings suppressed for MAUI compatibility", Scope = "namespaceanddescendants", Target = "~N:MapLocationApp")]

// Suppress async/await warnings
[assembly: SuppressMessage("Microsoft.Design", "CS1998:This async method lacks 'await' operators and will run synchronously", Justification = "Methods may be async for interface compliance or future implementation", Scope = "namespaceanddescendants", Target = "~N:MapLocationApp")]

// Suppress unused variable warnings
[assembly: SuppressMessage("Microsoft.Design", "CS0168:The variable is declared but never used", Justification = "Variables may be used in catch blocks for debugging", Scope = "namespaceanddescendants", Target = "~N:MapLocationApp")]

// Suppress NuGet package compatibility warnings
[assembly: SuppressMessage("Microsoft.Design", "NU1608:Detected package version outside of dependency constraint", Justification = "Using newer .NET version with compatible packages", Scope = "namespaceanddescendants", Target = "~N:MapLocationApp")]

// Suppress XAML binding warnings
[assembly: SuppressMessage("Microsoft.Design", "XC0022:Binding could be compiled to improve runtime performance", Justification = "Performance optimization not critical for this application", Scope = "namespaceanddescendants", Target = "~N:MapLocationApp")]