using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 選んだ Float 変数へ、手書きの計算式の結果を代入する。
/// 式は C# の小数演算。if や複数文は書けない。
/// </summary>
public class CustomCNode : BaseNode
{
    public const string Summary =
        "選んだ Float 変数へ、計算式の結果を代入する。式は + - * / % と括弧、小数、変数名。変数は Float / Int / Bool を数値として読む。未設定は 0。ゼロ除算や構文エラーは代入せず Next へ進む。";

    public string variableName = "A";
    public string expression = "0";

    public override void Execute(StoryPlayer player)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            Debug.LogWarning("【Custom C Node】代入先が空です。");
            player.ContinueTo(this, "Next");
            return;
        }

        if (!CustomCExpression.TryEvaluate(expression, ReadNumber, out float value, out string error))
        {
            Debug.LogWarning($"【Custom C Node】{variableName} = {expression} を計算できません。{error}");
            player.ContinueTo(this, "Next");
            return;
        }

        GameManager.SetFloat(variableName, value);
        string shown = value.ToString("0.########", CultureInfo.InvariantCulture);
        Debug.Log($"【Custom C Node】{variableName} = {expression} (結果: {shown})");
        player.ContinueTo(this, "Next");
    }

    private static float ReadNumber(string name)
    {
        if (GameManager.TryGetNumeric(name, out float number)) return number;
        return 0f;
    }

    public string GetDisplayTitle()
    {
        string formula = string.IsNullOrWhiteSpace(expression)
            ? "?"
            : expression.Replace("\r", " ").Replace("\n", " ").Trim();
        return $"{variableName} = {formula}";
    }
}

/// <summary>
/// C# の小数計算式を評価する。文、if、メソッド呼び出しは受け付けない。
/// </summary>
public static class CustomCExpression
{
    public static bool TryEvaluate(string source, Func<string, float> readVariable, out float value, out string error)
    {
        value = 0f;
        error = "";
        if (readVariable == null)
        {
            error = "変数の読み取りがありません。";
            return false;
        }

        Parser parser = new Parser(source ?? "", readVariable);
        return parser.TryParse(out value, out error);
    }

    private sealed class Parser
    {
        private readonly string source;
        private readonly Func<string, float> readVariable;
        private int index;

        public Parser(string source, Func<string, float> readVariable)
        {
            this.source = source;
            this.readVariable = readVariable;
        }

        public bool TryParse(out float value, out string error)
        {
            try
            {
                Skip();
                if (index >= source.Length)
                {
                    value = 0f;
                    error = "計算式が空です。";
                    return false;
                }

                value = ParseAdd();
                Skip();
                if (index < source.Length)
                {
                    value = 0f;
                    error = $"計算式の {index + 1} 文字目から先は式ではありません。";
                    return false;
                }

                error = "";
                return true;
            }
            catch (ExpressionException ex)
            {
                value = 0f;
                error = ex.Message;
                return false;
            }
            catch (OverflowException)
            {
                value = 0f;
                error = "計算結果が float の範囲を超えています。";
                return false;
            }
        }

        private float ParseAdd()
        {
            float value = ParseMul();
            while (true)
            {
                Skip();
                if (Match('+')) value = Check(value + ParseMul());
                else if (Match('-')) value = Check(value - ParseMul());
                else break;
            }

            return value;
        }

        private float ParseMul()
        {
            float value = ParseUnary();
            while (true)
            {
                Skip();
                if (Match('*'))
                {
                    value = Check(value * ParseUnary());
                }
                else if (Match('/'))
                {
                    float right = ParseUnary();
                    if (right == 0f) throw new ExpressionException("ゼロ除算です。");
                    value = Check(value / right);
                }
                else if (Match('%'))
                {
                    float right = ParseUnary();
                    if (right == 0f) throw new ExpressionException("ゼロ剰余です。");
                    value = Check(value % right);
                }
                else
                {
                    break;
                }
            }

            return value;
        }

        private float ParseUnary()
        {
            Skip();
            if (Match('+')) return ParseUnary();
            if (Match('-')) return Check(-ParseUnary());
            return ParsePrimary();
        }

        private float ParsePrimary()
        {
            Skip();
            if (index >= source.Length) throw new ExpressionException("計算式が途中で終わっています。");

            char c = source[index];
            if (c == '(')
            {
                index++;
                float value = ParseAdd();
                Skip();
                if (!Match(')')) throw new ExpressionException($"計算式の {index + 1} 文字目に ) が必要です。");
                return value;
            }

            if (char.IsDigit(c) || c == '.') return ParseNumber();
            if (IsIdentStart(c)) return Check(readVariable(ParseIdent()));

            throw new ExpressionException($"計算式の {index + 1} 文字目「{c}」は使えません。");
        }

        private float ParseNumber()
        {
            int start = index;
            bool sawDigit = false;
            bool sawDot = false;

            if (index < source.Length && source[index] == '.')
            {
                sawDot = true;
                index++;
            }

            while (index < source.Length && char.IsDigit(source[index]))
            {
                sawDigit = true;
                index++;
            }

            if (!sawDot && index < source.Length && source[index] == '.')
            {
                index++;
                while (index < source.Length && char.IsDigit(source[index]))
                {
                    sawDigit = true;
                    index++;
                }
            }

            if (index < source.Length && (source[index] == 'f' || source[index] == 'F')) index++;

            if (!sawDigit) throw new ExpressionException($"計算式の {start + 1} 文字目の数値を読めません。");

            string text = source.Substring(start, index - start);
            string numeric = text;
            if (numeric.EndsWith("f", StringComparison.OrdinalIgnoreCase))
            {
                numeric = numeric.Substring(0, numeric.Length - 1);
            }

            if (!float.TryParse(numeric, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out float value))
            {
                throw new ExpressionException($"数値 {text} は float の範囲を超えています。");
            }

            return Check(value);
        }

        private static float Check(float value)
        {
            if (!float.IsFinite(value)) throw new ExpressionException("計算結果が float の範囲を超えています。");
            return value;
        }

        private string ParseIdent()
        {
            int start = index;
            index++;
            while (index < source.Length && IsIdentPart(source[index])) index++;
            return source.Substring(start, index - start);
        }

        private bool Match(char c)
        {
            if (index < source.Length && source[index] == c)
            {
                index++;
                return true;
            }

            return false;
        }

        private void Skip()
        {
            while (index < source.Length && char.IsWhiteSpace(source[index])) index++;
        }

        private static bool IsIdentStart(char c)
        {
            return c == '_' || char.IsLetter(c);
        }

        private static bool IsIdentPart(char c)
        {
            return c == '_' || char.IsLetterOrDigit(c);
        }
    }

    private sealed class ExpressionException : Exception
    {
        public ExpressionException(string message) : base(message)
        {
        }
    }
}
