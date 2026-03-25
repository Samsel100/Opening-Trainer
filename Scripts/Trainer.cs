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

    [Export] public LineEdit addOpeningInput;
    [Export] public Button addOpeningButton;
    [Export] public OptionButton colorButton;

    [Export] public Button endRecordingButton;

    [Export] public Button nextButton;
    [Export] public Button previousButton;

    private Random rng = new();

    public ChessGame game = new();

    public const string OPENING_PATH = "user://openings.json";

    private Opening currentOpening;
    private Dictionary<string, Opening> openings;
    private List<List<string>> lines;
    private int currentLineIndex = 0;
    private int currentMoveIndex = 0;
    private int previousShuffleLineIndex = -1;

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
        {
            modeButton.AddItem(mode.ToString(), (int)mode);
        }
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

            case TrainingMode.ShowLines:
                break;

            case TrainingMode.FreeInput:
                HandleFreeInput(move);
                break;

            case TrainingMode.MultipleChoice:
                HandleMultipleChoice(move);
                break;

            case TrainingMode.Shuffle:
                HandleShuffle(move);
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

                lines = currentOpening.GetLines();
                currentLineIndex = 0;
                currentMoveIndex = 0;
                break;
            case TrainingMode.FreeInput:
                lines = currentOpening.GetLines();
                currentLineIndex = 0;
                currentMoveIndex = 0;

                if (currentOpening.Color == "b")
                {
                    NextMove();
                }
                break;

            case TrainingMode.MultipleChoice:
                lines = currentOpening.GetLines();
                currentLineIndex = 0;
                currentMoveIndex = 0;

                if (currentOpening.Color == "b")
                {
                    NextMove();
                }
                MakeMultipleChoices();

                break;
            case TrainingMode.Shuffle:
                lines = currentOpening.GetLines();
                LoadRandomPosition();
                break;
        }
    }

    private void HideUI()
    {
        RecordingLineGroup.Hide();
        endRecordingButton.Disabled = true;

        ShowLinesGroup.Hide();
        nextButton.Disabled = true;
        previousButton.Disabled = true;

        feedback.Text = "";
        retryButton.Hide();
        retryButton.Disabled = true;
    }

    private void SelectOpening(long index)
    {
        foreach (Opening opening in openings.Values)
        {
            if (opening.id == index)
            {
                SelectOpening(opening.Name);
                return;
            }
        }
    }

    private void SelectOpening(string name)
    {
        currentOpening = openings[name];
        if (mode != TrainingMode.None)
        {
            OnTrainingStarted();
        }

    }

    private void AddOpening()
    {
        string name = addOpeningInput.Text;
        if (name == "") return;
        string color = colorButton.GetItemId(colorButton.Selected) == 0 ? "w" : "b";
        if (!openings.Keys.Contains(name))
        {
            AddOpening(name, color);
        }

        addOpeningInput.Text = "";
    }

    private void AddOpening(string name, string color)
    {
        openings[name] = new Opening(name, color);
        int id = openings.Count;
        openings[name].id = id;
        pickOpeningButton.AddItem(name, id);
    }

    private void ApplyMoveString(string moveString)
    {
        var move = new Move(
            moveString.Substring(0, 2),
            moveString.Substring(2, 2),
            game.CurrentPlayer);

        game.MakeMove(move, true);
        board.SetupFromGame(game);
    }

    private void SetTrainingMode(long index)
    {
        SetTrainingMode((TrainingMode)index);
    }

    private void SetTrainingMode(TrainingMode mode)
    {
        this.mode = mode;
        if (currentOpening != null)
        {
            OnTrainingStarted();
        }
    }

    private void SaveAllOpenings()
    {
        Opening.SaveOpenings(OPENING_PATH, openings.Values.ToList());
    }

    private void ResetBoard(bool deleteArrows = true)
    {
        game = new ChessGame();
        game.Initialize();
        board.SetupFromGame(game);
        if (deleteArrows)
            board.DeleteAllArrows();
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

    private void Retry()
    {
        retryButton.Hide();
        retryButton.Disabled = true;

        feedback.Text = "";

        PreviousMove();
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

        currentOpening.AddLine(string.Join(" ", recordingLine));
        recordingLine.Clear();
    }

    private void NextMove()
    {
        if (lines.Count == 0) return;

        var line = lines[currentLineIndex];

        if (currentMoveIndex < line.Count)
        {
            ApplyMoveString(line[currentMoveIndex]);
            currentMoveIndex++;
        }
        else
        {
            currentLineIndex++;
            if (currentLineIndex >= lines.Count)
            {
                currentLineIndex = 0;
            }
            ResetBoard();

            currentMoveIndex = 0;
        }
    }

    private void PreviousMove()
    {
        if (lines.Count == 0) return;

        if (currentMoveIndex > 0)
        {
            currentMoveIndex--;
            RebuildCurrentLine();
        }
        else
        {
            currentLineIndex = (currentLineIndex - 1 + lines.Count) % lines.Count;
            var line = lines[currentLineIndex];

            currentMoveIndex = line.Count;
            RebuildCurrentLine();
        }
    }

    private void RebuildCurrentLine()
    {
        ResetBoard(false);

        var line = lines[currentLineIndex];

        for (int i = 0; i < currentMoveIndex; i++)
        {
            ApplyMoveString(line[i]);
        }
    }

    private void HandleFreeInput(Move move)
    {
        string correctMove = lines[currentLineIndex][currentMoveIndex];
        string attemptedMove = $"{move.OriginalPosition}{move.NewPosition}";

        if (attemptedMove == correctMove)
        {
            ApplyMoveString(correctMove);
            currentMoveIndex++;
            OnCorrectMove();
        }
        else
        {
            ApplyMoveString(attemptedMove);
            currentMoveIndex++;
            OnWrongMove();
            return;
        }

        NextMove();
        if (currentMoveIndex == 0 && currentOpening.Color == "b")
        {
            NextMove();
        }
    }

    private void HandleMultipleChoice(Move move)
    {
        string correctMove = lines[currentLineIndex][currentMoveIndex];
        string attemptedMove = $"{move.OriginalPosition}{move.NewPosition}";

        if (attemptedMove == correctMove)
        {
            ApplyMoveString(correctMove);
            currentMoveIndex++;
            OnCorrectMove();
        }
        else
        {
            ApplyMoveString(attemptedMove);
            currentMoveIndex++;
            OnWrongMove();
            return;
        }


        NextMove();
        if (currentMoveIndex == 0 && currentOpening.Color == "b")
        {
            NextMove();
        }

        MakeMultipleChoices();
    }

    private void MakeMultipleChoices()
    {
        board.DeleteAllArrows();
        List<string> choices = new();
        List<Move> validMoves = game.GetValidMoves(game.CurrentPlayer).ToList();
        Random rng = new();

        int count = Math.Min(4, validMoves.Count) - 1;

        for (int i = 0; i < count; i++)
        {
            int index;
            string choice;

            do
            {
                index = rng.Next(validMoves.Count);
                choice = $"{validMoves[index].OriginalPosition}{validMoves[index].NewPosition}";
            }
            while (choices.Contains(choice));

            choices.Add(choice);
        }

        choices.Add(lines[currentLineIndex][currentMoveIndex]);

        foreach (string choice in choices)
        {
            board.DrawArrow(choice);
        }
    }

    private void HandleShuffle(Move move)
    {
        string correctMove = lines[currentLineIndex][currentMoveIndex];
        string attemptedMove = $"{move.OriginalPosition}{move.NewPosition}";

        if (attemptedMove == correctMove)
        {
            ApplyMoveString(correctMove);
            OnCorrectMove();
        }
        else
        {
            ApplyMoveString(attemptedMove);
            currentMoveIndex++;
            OnWrongMove();
            return;
        }

        LoadRandomPosition();
    }

    private void LoadRandomPosition()
    {
        ResetBoard();

        do
        {
            currentLineIndex = rng.Next(lines.Count);

        } while (currentLineIndex == previousShuffleLineIndex && lines.Count > 1);

        previousShuffleLineIndex = currentLineIndex;

        var line = lines[currentLineIndex];

        currentMoveIndex = rng.Next(line.Count);

        while ((currentOpening.Color == "w" && currentMoveIndex % 2 != 0) || (currentOpening.Color == "b" && currentMoveIndex % 2 == 0))
            currentMoveIndex = rng.Next(line.Count);

        for (int i = 0; i < currentMoveIndex; i++)
        {
            ApplyMoveString(line[i]);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            SaveAllOpenings();
        }
    }
}