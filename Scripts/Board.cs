using Godot;
using System;
using ChessDotNetCore;
using System.Collections.Generic;

public partial class Board : Node2D
{
    [Export] public TileMapLayer tileMap;
    [Export] public Trainer trainer;
    [Export] public Godot.Collections.Dictionary<string, Vector2I> pieceTiles;
    [Export] private PackedScene arrowPrefab;
    private Vector2I? clikedTile = null;
    private List<Node2D> arrows = new();

    public void SetupFromGame(ChessGame game)
    {
        tileMap.Clear();

        string fen = game.GetFen();
        string boardPart = fen.Split(' ')[0];

        int x = 0;
        int y = 0;

        foreach (char c in boardPart)
        {
            if (c == '/')
            {
                y++;
                x = 0;
                continue;
            }

            if (char.IsDigit(c))
            {
                x += c - '0';
                continue;
            }

            string key = FenToKey(c);

            if (pieceTiles.TryGetValue(key, out Vector2I atlasCoords))
            {
                tileMap.SetCell(new Vector2I(x, y), 0, atlasCoords);
            }

            x++;
        }
    }

    private string FenToKey(char c)
    {
        bool isWhite = char.IsUpper(c);
        char piece = char.ToLower(c);

        string color = isWhite ? "w" : "b";

        return $"{color}{piece}";
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("click"))
        {
            Vector2 localMousePos = tileMap.GetLocalMousePosition();
            Vector2I tilePosition = tileMap.LocalToMap(localMousePos);

            if (tilePosition.X >= 0 && tilePosition.X < 8 &&
                tilePosition.Y >= 0 && tilePosition.Y < 8)
            {
                if (clikedTile == null)
                {
                    clikedTile = tilePosition;
                }
                else
                {
                    Vector2I from = clikedTile.Value;
                    Vector2I to = tilePosition;

                    string fromPos = $"{(char)('a' + from.X)}{8 - from.Y}";
                    string toPos = $"{(char)('a' + to.X)}{8 - to.Y}";

                    Move move = new(fromPos, toPos, trainer.game.CurrentPlayer);
                    trainer.OnMoveAttempted(move);

                    clikedTile = null;
                }
            }
        }
    }

    public void DrawArrow(string move)
    {
        if (move.Length < 4) return;

        Vector2I from = new(
            move[0] - 'a',
            8 - (move[1] - '0')
        );

        Vector2I to = new(
            move[2] - 'a',
            8 - (move[3] - '0')
        );

        Vector2 fromPos = tileMap.MapToLocal(from) + tileMap.TileSet.TileSize / 2;
        Vector2 toPos = tileMap.MapToLocal(to) + tileMap.TileSet.TileSize / 2;

        Vector2 dir = toPos - fromPos;
        float length = dir.Length();

        Node2D arrow = arrowPrefab.Instantiate<Node2D>();

        arrow.Position = fromPos - Vector2.One * 4.5f * 16;

        float angle = Vector2.Up.AngleTo(dir.Normalized());
        arrow.Rotation = angle;

        Vector2 scale = arrow.Scale;
        scale.Y = length / 16;
        arrow.Scale = scale;

        AddChild(arrow);
        arrows.Add(arrow);
    }

    public void DeleteAllArrows()
    {
        foreach (Node2D arrow in arrows)
        {
            arrow.QueueFree();
        }

        arrows.Clear();
    }
}
