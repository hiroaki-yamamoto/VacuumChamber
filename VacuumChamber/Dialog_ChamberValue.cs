using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VacuumChamber
{
  /// <summary>
  /// Game-pausing modal dialog featuring <c>[-]</c>, numeric text input, and <c>[+]</c> controls
  /// for adjusting either the target vacuum percentage or the pumping speed level.
  /// </summary>
  public class Dialog_ChamberValue : Window
  {
    private readonly string title;
    private readonly int minValue;
    private readonly int maxValue;
    private readonly int shiftStep;
    private readonly string unitSuffix;
    private readonly Func<int, string>? summaryFormatter;
    private readonly Action<int> onConfirm;

    private int currentValue;
    private string editBuffer;

    /// <inheritdoc />
    public override Vector2 InitialSize => new Vector2(360f, 196f);

    /// <summary>
    /// Initializes a new <see cref="Dialog_ChamberValue"/> modal window that pauses the simulation while open.
    /// </summary>
    /// <param name="title">Localized dialog header title.</param>
    /// <param name="initialValue">Starting integer value.</param>
    /// <param name="minValue">Inclusive minimum bound.</param>
    /// <param name="maxValue">Inclusive maximum bound.</param>
    /// <param name="shiftStep">Step increment/decrement when Shift is held.</param>
    /// <param name="unitSuffix">Optional unit suffix shown after the numeric field.</param>
    /// <param name="summaryFormatter">Optional formatter for the live preview subtitle.</param>
    /// <param name="onConfirm">Callback invoked with the clamped integer on confirmation.</param>
    public Dialog_ChamberValue(
      string title,
      int initialValue,
      int minValue,
      int maxValue,
      int shiftStep,
      string unitSuffix,
      Func<int, string>? summaryFormatter,
      Action<int> onConfirm)
    {
      this.title = title;
      this.minValue = minValue;
      this.maxValue = Math.Max(minValue, maxValue);
      this.shiftStep = Math.Max(1, shiftStep);
      this.unitSuffix = unitSuffix ?? string.Empty;
      this.summaryFormatter = summaryFormatter;
      this.onConfirm = onConfirm ?? throw new ArgumentNullException(nameof(onConfirm));

      currentValue = Mathf.Clamp(initialValue, this.minValue, this.maxValue);
      editBuffer = currentValue.ToString();

      forcePause = true;
      absorbInputAroundWindow = true;
      doCloseX = true;
      closeOnAccept = true;
      closeOnCancel = true;
      onlyOneOfTypeAllowed = true;
    }

    /// <inheritdoc />
    public override void DoWindowContents(Rect inRect)
    {
      TextAnchor prevAnchor = Text.Anchor;
      GameFont prevFont = Text.Font;

      try
      {
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.UpperLeft;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), title);

        Text.Font = GameFont.Small;
        if (summaryFormatter != null)
        {
          GUI.color = ColoredText.SubtleGrayColor;
          Widgets.Label(
            new Rect(inRect.x, inRect.y + 34f, inRect.width, 24f),
            summaryFormatter(GetEffectiveValue()));
          GUI.color = Color.white;
        }

        float rowY = inRect.y + 68f;
        float rowHeight = 34f;
        float stepButtonWidth = 44f;
        float gap = 8f;
        float unitWidth = string.IsNullOrEmpty(unitSuffix) ? 0f : 32f;
        float inputWidth = inRect.width - (stepButtonWidth * 2f) - (gap * 2f) - unitWidth;

        Rect minusRect = new Rect(inRect.x, rowY, stepButtonWidth, rowHeight);
        Rect inputRect = new Rect(minusRect.xMax + gap, rowY, inputWidth, rowHeight);
        Rect unitRect = new Rect(inputRect.xMax + 4f, rowY, Math.Max(0f, unitWidth - 4f), rowHeight);
        Rect plusRect = new Rect(inRect.xMax - stepButtonWidth, rowY, stepButtonWidth, rowHeight);

        int step = Event.current.shift ? shiftStep : 1;

        if (Widgets.ButtonText(minusRect, "-"))
        {
          SoundDefOf.Click.PlayOneShotOnCamera();
          SetValue(GetEffectiveValue() - step);
        }

        Widgets.TextFieldNumeric(inputRect, ref currentValue, ref editBuffer, minValue, maxValue);

        if (!string.IsNullOrEmpty(unitSuffix))
        {
          Text.Anchor = TextAnchor.MiddleLeft;
          Widgets.Label(unitRect, unitSuffix);
          Text.Anchor = TextAnchor.UpperLeft;
        }

        if (Widgets.ButtonText(plusRect, "+"))
        {
          SoundDefOf.Click.PlayOneShotOnCamera();
          SetValue(GetEffectiveValue() + step);
        }

        float buttonWidth = 110f;
        float buttonHeight = 34f;
        float footerY = inRect.yMax - buttonHeight;

        Rect cancelRect = new Rect(inRect.x, footerY, buttonWidth, buttonHeight);
        Rect confirmRect = new Rect(inRect.xMax - buttonWidth, footerY, buttonWidth, buttonHeight);

        if (Widgets.ButtonText(cancelRect, "VC_Dialog_Cancel".Translate()))
        {
          Close();
          return;
        }

        if (Widgets.ButtonText(confirmRect, "VC_Dialog_Confirm".Translate()))
        {
          CommitAndClose();
        }
      }
      finally
      {
        Text.Font = prevFont;
        Text.Anchor = prevAnchor;
        GUI.color = Color.white;
      }
    }

    /// <inheritdoc />
    public override void OnAcceptKeyPressed()
    {
      CommitAndClose();
      Event.current.Use();
    }

    private int GetEffectiveValue()
    {
      if (int.TryParse(editBuffer, out int parsed))
      {
        return Mathf.Clamp(parsed, minValue, maxValue);
      }

      return Mathf.Clamp(currentValue, minValue, maxValue);
    }

    private void SetValue(int newValue)
    {
      currentValue = Mathf.Clamp(newValue, minValue, maxValue);
      editBuffer = currentValue.ToString();
    }

    private void CommitAndClose()
    {
      int finalValue = GetEffectiveValue();
      onConfirm(finalValue);
      Close();
    }
  }
}
