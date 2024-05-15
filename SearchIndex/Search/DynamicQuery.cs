using System;
using System.Data.Common;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Text;

namespace Kennedy.SearchIndex.Search;

/// <summary>
/// Dynamically construct a SQL query, with parameters, and get a Formattable string
/// This is helpful when you have a variable number of where clauses, or joins that
/// need to happen based on WHERE clauses
/// </summary>
/// <typeparam name="T"></typeparam>
public class DynamicQuery<T> where T : DbParameter, new()
{
    ArrayList parametersList = new ArrayList();

    StringBuilder query = new StringBuilder();

    int paramNumber = 0;

    bool HasWhereCondition = false;

    public void Append(string q)
    {
        if (paramNumber != parametersList.Count)
        {
            throw new ApplicationException("Must add parameters before appending more query text");
        }

        while (q.Contains("{}"))
        {
            q = ReplaceFirst(q, "{}", "{" + paramNumber + "}");
            paramNumber++;
        }
        query.Append(q);
    }

    /// <summary>
    /// Appens a where condition. Automaticallu handles adding "AND" if needed
    /// </summary>
    /// <param name="q"></param>
    public void AppendWhereCondition(string q)
    {
        if (!HasWhereCondition)
        {
            HasWhereCondition = true;
            Append(q);
        }
        else
        {
            q = "AND " + q;
            Append(q);
        }
    }

    public void AddParameters(params T[] parameters)
    {
        foreach (var parameter in parameters)
        {
            AddParameter(parameter);
        }
    }

    public void AddParameter(T parameter)
    {
        parametersList.Add(parameter);
    }

    public void AddParameter(string name, object? value)
    {
        var param = new T();
        param.ParameterName = name;
        param.Value = value;
        AddParameter(param);
    }

    public FormattableString GetFormattableString()
    {
        var parameters = parametersList.ToArray();
        if (paramNumber != parameters.Length)
        {
            throw new ApplicationException("Cannot create SQL query. Parameter count doesn't match placeholder count!");
        }

        return FormattableStringFactory.Create(query.ToString(), parametersList.ToArray());
    }

    private string ReplaceFirst(string text, string search, string replace)
    {
        int pos = text.IndexOf(search);
        if (pos < 0)
        {
            return text;
        }
        return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
    }
}