using Scp603.Core.Numerics;

namespace Scp603.Core.Morphik;

/// <summary>Raised when a Morphik program does something illegal at run time.</summary>
public sealed class MorphikRuntimeException : Exception
{
    public MorphikRuntimeException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Executes compiled Morphik bytecode. <see cref="StepLimit"/> keeps the "main thread"
/// from running away in tests and in the safe simulation.
/// </summary>
public sealed class MorphikVm
{
    private readonly Stack<object?> _stack = new();
    private readonly Dictionary<string, object?> _globals = new(StringComparer.Ordinal);

    public long StepLimit { get; set; } = long.MaxValue;

    public long Steps { get; private set; }

    public void Run(MorphikBytecode bytecode, IMorphikHost host)
    {
        Instruction[] code = bytecode.Instructions;
        int ip = 0;
        Steps = 0;

        while (ip >= 0 && ip < code.Length)
        {
            if (host.ShouldStop || Steps >= StepLimit)
            {
                return;
            }

            Steps++;
            Instruction ins = code[ip];
            ip++;

            switch (ins.Op)
            {
                case BcOp.Nop:
                    break;

                case BcOp.PushNum:
                    _stack.Push(ins.A);
                    break;

                case BcOp.PushStr:
                    _stack.Push(ins.S ?? string.Empty);
                    break;

                case BcOp.Define:
                case BcOp.Store:
                    _globals[ins.S!] = _stack.Pop();
                    break;

                case BcOp.Load:
                    if (!_globals.TryGetValue(ins.S!, out object? value))
                    {
                        throw new MorphikRuntimeException($"Неизвестная переменная '{ins.S}'");
                    }

                    _stack.Push(value);
                    break;

                case BcOp.Add:
                case BcOp.Sub:
                case BcOp.Mul:
                case BcOp.Div:
                case BcOp.Mod:
                    BinaryArithmetic(ins.Op);
                    break;

                case BcOp.Eq:
                case BcOp.Ne:
                case BcOp.Lt:
                case BcOp.Gt:
                case BcOp.Le:
                case BcOp.Ge:
                    Compare(ins.Op);
                    break;

                case BcOp.And:
                    {
                        long b = ToLong(_stack.Pop());
                        long a = ToLong(_stack.Pop());
                        _stack.Push((a != 0 && b != 0) ? 1L : 0L);
                        break;
                    }

                case BcOp.Or:
                    {
                        long b = ToLong(_stack.Pop());
                        long a = ToLong(_stack.Pop());
                        _stack.Push((a != 0 || b != 0) ? 1L : 0L);
                        break;
                    }

                case BcOp.Not:
                    _stack.Push(ToLong(_stack.Pop()) == 0 ? 1L : 0L);
                    break;

                case BcOp.Neg:
                    _stack.Push(-ToLong(_stack.Pop()));
                    break;

                case BcOp.Call:
                    Invoke(ins.S!, (int)ins.A);
                    break;

                case BcOp.Emit:
                    if (ins.A == 0)
                    {
                        host.Emit(null);
                    }
                    else
                    {
                        host.Emit(_stack.Pop());
                    }

                    break;

                case BcOp.Window:
                    if (ins.A == 0)
                    {
                        host.Window(null);
                    }
                    else
                    {
                        host.Window(_stack.Pop());
                    }

                    break;

                case BcOp.Pop:
                    _stack.Pop();
                    break;

                case BcOp.Jump:
                    ip = (int)ins.A;
                    break;

                case BcOp.JumpIfFalse:
                    {
                        long cond = ToLong(_stack.Pop());
                        if (cond == 0)
                        {
                            ip = (int)ins.A;
                        }

                        break;
                    }

                case BcOp.Return:
                case BcOp.Halt:
                    return;

                case BcOp.Break:
                    return;

                default:
                    throw new MorphikRuntimeException($"Неизвестная инструкция {ins.Op}");
            }
        }
    }

    private void BinaryArithmetic(BcOp op)
    {
        long b = ToLong(_stack.Pop());
        long a = ToLong(_stack.Pop());
        long result = op switch
        {
            BcOp.Add => unchecked(a + b),
            BcOp.Sub => unchecked(a - b),
            BcOp.Mul => unchecked(a * b),
            BcOp.Div => b == 0 ? 0 : a / b,
            BcOp.Mod => b == 0 ? 0 : a % b,
            _ => 0,
        };
        _stack.Push(result);
    }

    private void Compare(BcOp op)
    {
        long b = ToLong(_stack.Pop());
        long a = ToLong(_stack.Pop());
        bool result = op switch
        {
            BcOp.Eq => a == b,
            BcOp.Ne => a != b,
            BcOp.Lt => a < b,
            BcOp.Gt => a > b,
            BcOp.Le => a <= b,
            BcOp.Ge => a >= b,
            _ => false,
        };
        _stack.Push(result ? 1L : 0L);
    }

    private void Invoke(string name, int argCount)
    {
        var args = new long[argCount];
        for (int i = argCount - 1; i >= 0; i--)
        {
            args[i] = ToLong(_stack.Pop());
        }

        long result = name switch
        {
            "isprime" => Primes.IsPrime(args[0]) ? 1L : 0L,
            "nextprime" => NextPrime(args[0]),
            "prevprime" => PrevPrime(args[0]),
            "primeat" => Primes.Nth((int)Math.Clamp(args[0], 0, 1_000_000)),
            "indexof" => Primes.IndexOf(args[0]),
            "inc" => args[0] + 1,
            "dec" => args[0] - 1,
            "abs" => Math.Abs(args[0]),
            "min" => Math.Min(args[0], args[1]),
            "max" => Math.Max(args[0], args[1]),
            _ => throw new MorphikRuntimeException($"Неизвестная функция '{name}'"),
        };

        _stack.Push(result);
    }

    private static long NextPrime(long value)
    {
        long candidate = value < 2 ? 2 : value + 1;
        while (!Primes.IsPrime(candidate))
        {
            candidate++;
        }

        return candidate;
    }

    private static long PrevPrime(long value)
    {
        long candidate = value - 1;
        while (candidate >= 2 && !Primes.IsPrime(candidate))
        {
            candidate--;
        }

        return candidate < 2 ? 2 : candidate;
    }

    private static long ToLong(object? value) => value switch
    {
        null => 0,
        long l => l,
        int i => i,
        string s when long.TryParse(s, out long parsed) => parsed,
        bool b => b ? 1 : 0,
        _ => 0,
    };
}
