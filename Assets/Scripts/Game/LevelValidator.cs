using System.Collections.Generic;
using UnityEngine;

public class LevelValidationReport
{
    public bool ok;
    public int validCount;
    public readonly List<string> errors = new List<string>();
    public readonly List<SpecimenDefinition> validSpecimens = new List<SpecimenDefinition>();
}

/// <summary>
/// Checks a data-only level before anything is spawned. Invalid entries are
/// reported and skipped; one bad record must never stop the rest of the level.
/// </summary>
public static class LevelValidator
{
    public static LevelValidationReport Validate(LevelDefinition level)
    {
        var report = new LevelValidationReport();
        if (level == null)
        {
            report.errors.Add("LevelDefinition отсутствует.");
            return report;
        }

        var speciesValues = new HashSet<DinoSpecies>();
        var speciesIds = new HashSet<string>();
        if (level.species == null || level.species.Count == 0)
            report.errors.Add("Не задан список видов для книги.");
        else
        {
            foreach (var s in level.species)
            {
                if (s == null)
                {
                    report.errors.Add("В списке видов есть пустая запись.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(s.id))
                    report.errors.Add("У вида пустой id: " + s.name);
                else if (!speciesIds.Add(s.id))
                    report.errors.Add("Повторяющийся id вида: " + s.id);
                if (!speciesValues.Add(s.speciesValue))
                    report.errors.Add("Вид " + s.displayName + " повторяется.");
                if (string.IsNullOrWhiteSpace(s.displayName))
                    report.errors.Add("У вида нет названия: " + s.name);
                if (s.mainArt == null)
                    report.errors.Add("Нет картинки для книги: " + s.displayName);
                if (string.IsNullOrWhiteSpace(s.description))
                    report.errors.Add("Нет описания для книги: " + s.displayName);
            }
        }

        var ids = new HashSet<string>();
        if (level.specimens == null || level.specimens.Count == 0)
            report.errors.Add("В уровне нет ни одного образца.");
        else
        {
            foreach (var d in level.specimens)
            {
                if (d == null)
                {
                    report.errors.Add("В списке образцов есть пустая запись.");
                    continue;
                }

                bool valid = true;
                if (string.IsNullOrWhiteSpace(d.id))
                {
                    report.errors.Add("У образца пустой id: " + d.name);
                    valid = false;
                }
                else if (!ids.Add(d.id))
                {
                    report.errors.Add("Повторяющийся id образца: " + d.id);
                    valid = false;
                }
                if (string.IsNullOrWhiteSpace(d.partName))
                {
                    report.errors.Add("У образца нет части скелета: " + d.name);
                    valid = false;
                }
                if (d.bonePrefab == null && d.completeMesh == null)
                {
                    report.errors.Add("У образца нет меша/префаба: " + d.name);
                    valid = false;
                }
                if (!speciesValues.Contains(d.species))
                {
                    report.errors.Add("У образца неизвестный вид: " + d.name);
                    valid = false;
                }
                if (string.IsNullOrWhiteSpace(d.tagText))
                {
                    report.errors.Add("У образца пустая бирка: " + d.name);
                    valid = false;
                }

                if (valid)
                {
                    report.validSpecimens.Add(d);
                    report.validCount++;
                }
            }
        }

        report.ok = report.errors.Count == 0 && report.validCount > 0;
        return report;
    }
}
