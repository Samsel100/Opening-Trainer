using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using Godot;


public class Opening
{
    public string Name { get; set; }
    public string Color { get; set; }
    public Node Root { get; set; }
    public int id;

    public Opening() { }

    public Opening(string name, string color = "b")
    {
        Name = name;
        Color = color;
        Root = new Node("");
    }

    public void AddLine(string sanMoves)
    {
        Node current = Root;

        foreach (var san in sanMoves.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!current.Children.TryGetValue(san, out var next))
            {
                next = new Node(san);
                current.Children[san] = next;
            }

            current = next;
        }
    }

    public List<List<string>> GetLines()
    {
        var result = new List<List<string>>();
        var current = new List<string>();

        DFS(Root, current, result);
        return result;
    }

    private void DFS(Node node, List<string> current, List<List<string>> result)
    {
        if (node.Children.Count == 0)
        {
            if (current.Count > 0)
                result.Add(new List<string>(current));
            return;
        }

        foreach (var child in node.Children.Values)
        {
            current.Add(child.Move);
            DFS(child, current, result);
            current.RemoveAt(current.Count - 1);
        }
    }

    public static void SaveOpenings(string path, List<Opening> openings)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        string json = JsonSerializer.Serialize(openings, options);

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.StoreString(json);
    }

    public static Dictionary<string, Opening> LoadOpenings(string path)
    {
        if (!FileAccess.FileExists(path))
            return new Dictionary<string, Opening>();

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        string json = file.GetAsText();

        var list = JsonSerializer.Deserialize<List<Opening>>(json)
                   ?? new List<Opening>();

        return list.ToDictionary(o => o.Name, o => o);
    }
}

public class Node
{
    public string Move { get; set; }
    public Dictionary<string, Node> Children { get; set; }

    public Node() { }

    public Node(string move)
    {
        Move = move;
        Children = new Dictionary<string, Node>();
    }
}