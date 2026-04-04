using System.Collections.Generic;
using System.Linq;
using System;
using ChessDotNetCore;
using Godot;

public partial class Trainer : Node2D
{
    [Export] public Board board;
    public TrainingMode mode = TrainingMode.None;

    [Export] public Control RecordingLineGroup;
    [Export] public Control ShowLinesGroup;

    [Export] public Label title;
    [Export] public Label feedback;
    [Export] public Button retryButton;

    [Export] public OptionButton pickOpeningButton;
    [Export] public OptionButton modeButton;

    [Export] public Tree linesTree;

    [Export] public LineEdit addOpeningInput;
    [Export] public Button addOpeningButton;
    [Export] public OptionButton colorButton;

    [Export] public LineEdit lineNameInput;
    [Export] public Button endRecordingButton;

    [Export] public Button nextButton;
    [Export] public Button previousButton;

    private Random rng = new();

    public ChessGame game = new();

    public const string OPENING_PATH = "user://openings.json";

    private Opening currentOpening;
    private Dictionary<string, Opening> openings;

    private Node currentNode;
    private List<Node> history = new();

    private List<string> recordingLine = new();

    public enum TrainingMode
    {
        RecordingLine,
        ShowLines,
        FreeInput,
        MultipleChoice,
        Shuffle,
        None,
    }

    public override void _Ready()
    {
        openings = Opening.LoadOpenings(OPENING_PATH);
        ResetBoard();

        foreach (TrainingMode mode in Enum.GetValues(typeof(TrainingMode)))
            modeButton.AddItem(mode.ToString(), (int)mode);

        modeButton.ItemSelected += SetTrainingMode;
        modeButton.Select((int)TrainingMode.None);

        pickOpeningButton.AddItem("None", -1);

        int id = 1;
        foreach (Opening opening in openings.Values)
        {
            opening.id = id;
            pickOpeningButton.AddItem(opening.Name, id);
            id++;
        }

        pickOpeningButton.ItemSelected += SelectOpening;
        addOpeningButton.ButtonDown += AddOpening;
        endRecordingButton.ButtonDown += EndRecording;
        nextButton.ButtonDown += NextMove;
        previousButton.ButtonDown += PreviousMove;
        retryButton.ButtonDown += Retry;

        HideUI();
    }

    public void OnMoveAttempted(Move move)
    {
        if (!game.IsValidMove(move))
            return;

        switch (mode)
        {
            case TrainingMode.RecordingLine:
                RecordMove(move);
                break;
            case TrainingMode.FreeInput:
                HandleFreeInput(move);
                break;
            case TrainingMode.MultipleChoice:
                HandleMultipleChoice(move);
                break;
            default:
                game.MakeMove(move, true);
                board.SetupFromGame(game);
                break;
        }
    }

    private void OnTrainingStarted()
    {
        HideUI();
        ResetBoard();

        title.Text = $"Training the {currentOpening.Name}";

        currentNode = currentOpening.Root;
        history.Clear();

        switch (mode)
        {
            case TrainingMode.RecordingLine:
                RecordingLineGroup.Show();
                endRecordingButton.Disabled = false;
                break;

            case TrainingMode.ShowLines:
                ShowLinesGroup.Show();
                nextButton.Disabled = false;
                previousButton.Disabled = false;
                break;

            case TrainingMode.FreeInput:
            case TrainingMode.MultipleChoice:
                if (currentOpening.Color == "b")
                    NextMove();

                if (mode == TrainingMode.MultipleChoice)
                    MakeMultipleChoices();
                break;
        }
    }

    private void HideUI()
    {
        RecordingLineGroup.Hide();
        ShowLinesGroup.Hide();

        nextButton.Disabled = true;
        previousButton.Disabled = true;
        endRecordingButton.Disabled = true;

        feedback.Text = "";
        retryButton.Hide();
        retryButton.Disabled = true;
    }

    private void ResetBoard(bool deleteArrows = true)
    {
        game = new ChessGame();
        game.Initialize();
        board.SetupFromGame(game);

        if (deleteArrows)
            board.DeleteAllArrows();
    }

    private void RebuildFromHistory()
    {
        ResetBoard(false);

        currentNode = currentOpening.Root;

        foreach (var node in history)
        {
            ApplyMoveString(node.Move);
            currentNode = node;
        }
    }

    private void ApplyMoveString(string moveString)
    {
        var move = new Move(
            moveString.Substring(0, 2),
            moveString.Substring(2, 2),
            game.CurrentPlayer
        );

        game.MakeMove(move, true);
        board.SetupFromGame(game);
    }

    private void ApplyNode(Node node)
    {
        ApplyMoveString(node.Move);
        history.Add(node);
        currentNode = node;
    }

    private void NextMove()
    {
        if (currentNode.Children.Count == 0)
        {
            ResetBoard();
            currentNode = currentOpening.Root;
            history.Clear();
            return;
        }

        var next = currentNode.Children.Values.First();
        ApplyNode(next);
    }

    private void PreviousMove()
    {
        if (history.Count == 0)
            return;

        history.RemoveAt(history.Count - 1);
        RebuildFromHistory();
    }

    private void Retry()
    {
        retryButton.Hide();
        retryButton.Disabled = true;
        feedback.Text = "";

        RebuildFromHistory();
    }

    private void OnWrongMove()
    {
        feedback.Text = "Wrong!";
        retryButton.Show();
        retryButton.Disabled = false;
    }

    private void OnCorrectMove()
    {
        feedback.Text = "Correct!";
    }

    private void RecordMove(Move move)
    {
        string moveString = $"{move.OriginalPosition}{move.NewPosition}";

        game.MakeMove(move, true);
        board.SetupFromGame(game);

        recordingLine.Add(moveString);
    }

    private void EndRecording()
    {
        if (recordingLine.Count == 0)
            return;

        string name = lineNameInput.Text;
        if (string.IsNullOrEmpty(name))
            name = "Unnamed line";

        currentOpening.AddLine(string.Join(" ", recordingLine), name);

        recordingLine.Clear();
        lineNameInput.Text = "";
    }

    private void HandleFreeInput(Move move)
    {
        string attempted = $"{move.OriginalPosition}{move.NewPosition}";

        if (currentNode.Children.TryGetValue(attempted, out var next))
        {
            ApplyNode(next);
            OnCorrectMove();
            NextMove();
        }
        else
        {
            ApplyMoveString(attempted);
            OnWrongMove();
        }
    }

    private void HandleMultipleChoice(Move move)
    {
        string attempted = $"{move.OriginalPosition}{move.NewPosition}";

        if (currentNode.Children.TryGetValue(attempted, out var next))
        {
            ApplyNode(next);
            OnCorrectMove();
            NextMove();
        }
        else
        {
            ApplyMoveString(attempted);
            OnWrongMove();
            return;
        }

        MakeMultipleChoices();
    }

    private void MakeMultipleChoices()
    {
        board.DeleteAllArrows();

        List<string> choices = new();
        var validMoves = game.GetValidMoves(game.CurrentPlayer).ToList();

        int count = Math.Min(4, validMoves.Count) - 1;

        for (int i = 0; i < count; i++)
        {
            string choice;
            do
            {
                var m = validMoves[rng.Next(validMoves.Count)];
                choice = $"{m.OriginalPosition}{m.NewPosition}";
            }
            while (choices.Contains(choice));

            choices.Add(choice);
        }

        choices.AddRange(currentNode.Children.Keys);

        foreach (var c in choices)
            board.DrawArrow(c);
    }

    private void BuildLinesTree()
    {
        linesTree.Clear();

        TreeItem rootItem = linesTree.CreateItem();

        if (currentOpening == null || currentOpening.Root == null)
            return;

        foreach (var child in currentOpening.Root.Children.Values)
            AddNodeToTree(child, rootItem, "", currentOpening.Root.LineName);
    }

    private void AddNodeToTree(Node node, TreeItem parentItem, string path, string parentLineName)
    {
        string newPath = string.IsNullOrEmpty(path)
            ? node.Move
            : path + " " + node.Move;

        TreeItem currentParent = parentItem;

        bool isDifferentFromParent = (node.LineName ?? "") != (parentLineName ?? "");

        if (isDifferentFromParent)
        {
            TreeItem item = linesTree.CreateItem(parentItem);

            string display = string.IsNullOrEmpty(node.LineName)
                ? node.Move
                : node.LineName;

            item.SetText(0, display);
            item.SetMetadata(0, newPath);

            currentParent = item;
        }

        foreach (var child in node.Children.Values)
            AddNodeToTree(child, currentParent, newPath, node.LineName);
    }

    private void SelectOpening(long index)
    {
        foreach (var o in openings.Values)
            if (o.id == index)
                SelectOpening(o.Name);

        BuildLinesTree();
    }

    private void SelectOpening(string name)
    {
        currentOpening = openings[name];

        if (mode != TrainingMode.None)
            OnTrainingStarted();
    }

    private void AddOpening()
    {
        string name = addOpeningInput.Text;
        if (name == "") return;

        string color = colorButton.GetItemId(colorButton.Selected) == 0 ? "w" : "b";

        if (!openings.ContainsKey(name))
        {
            openings[name] = new Opening(name, color);
            openings[name].id = openings.Count;
            pickOpeningButton.AddItem(name, openings[name].id);
        }

        addOpeningInput.Text = "";
    }

    private void SetTrainingMode(long index)
    {
        SetTrainingMode((TrainingMode)index);
    }

    private void SetTrainingMode(TrainingMode mode)
    {
        this.mode = mode;

        if (currentOpening != null)
            OnTrainingStarted();
    }

    private void SaveAllOpenings()
    {
        Opening.SaveOpenings(OPENING_PATH, openings.Values.ToList());
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
            SaveAllOpenings();
    }
}