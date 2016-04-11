﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using betteribttest.FlowAnalysis;
using betteribttest.GMAst;

namespace betteribttest.Dissasembler
{

    public static class ILAstBuilderExtensions
    {
        public static void DebugPrintILAst(this IEnumerable<ILNode> nodes, string filename)
        {
            int labelMax = 0;
            foreach (var n in nodes.OfType<ILLabel>()) if (n.ToString().Length > labelMax) labelMax = n.ToString().Length;
            using (StreamWriter sw = new StreamWriter(filename))
            {
                PlainTextWriter ptw = new PlainTextWriter(sw);
                ptw.Header = new string(' ', labelMax + 2); // fill up header
                bool inLabel = false;
                foreach (var i in nodes)
                {
                    if (i is ILLabel)
                    {
                        if (inLabel) ptw.WriteLine();
                        ptw.Header = i.ToString();
                        inLabel = true;
                    }
                    else
                    {
                        i.WriteTo(ptw);
                        ptw.WriteLine();
                        inLabel = false;
                        ptw.Header = null;
                    }
                }
            }

        }
        public static bool isExpression(this GMCode i)
        {
            switch (i)
            {
                case GMCode.Neg:
                case GMCode.Not:
                case GMCode.Add:
                case GMCode.Sub:
                case GMCode.Mul:
                case GMCode.Div:
                case GMCode.Mod:
                case GMCode.And:
                case GMCode.Or:
                case GMCode.Xor:
                case GMCode.Sal:
                case GMCode.Seq:
                case GMCode.Sge:
                case GMCode.Sgt:
                case GMCode.Sle:
                case GMCode.Slt:
                case GMCode.Sne:
                    return true;
                default:
                    return false;
            }
        }
        public static int GetPopDelta(this GMCode i)
        {
            switch (i)
            {
                case GMCode.Popenv:
                case GMCode.Exit:
                case GMCode.Conv:
                    break; // we ignore conv
                case GMCode.Call:
                case GMCode.Push:
                case GMCode.Pop:
                case GMCode.Dup:
                    throw new Exception("Need more info for pop");
                case GMCode.Popz:
                case GMCode.Ret:
                case GMCode.B:
                case GMCode.Bt:
                case GMCode.Bf:
                case GMCode.Neg:
                case GMCode.Not:
                case GMCode.Pushenv:
                    return 1;
                case GMCode.Add:
                case GMCode.Sub:
                case GMCode.Mul:
                case GMCode.Div:
                case GMCode.Mod:
                case GMCode.And:
                case GMCode.Or:
                case GMCode.Xor:
                case GMCode.Sal:
                case GMCode.Seq:
                case GMCode.Sge:
                case GMCode.Sgt:
                case GMCode.Sle:
                case GMCode.Slt:
                case GMCode.Sne:
                    return 2;
                case GMCode.Var:
                case GMCode.Constant:
                    return 0;
                default:
                    throw new Exception("Unkonwn opcode");
            }
            return 0;
        }
        public static int GetPushDelta(this GMCode code)
        {
            switch (code)
            {
                case GMCode.Popenv:
                case GMCode.Exit:
                case GMCode.Conv:
                    break; // we ignore conv
                case GMCode.Call:
                case GMCode.Push:
                    return 1;
                case GMCode.Pop:
                case GMCode.Popz:
                case GMCode.B:
                case GMCode.Bt:
                case GMCode.Bf:
                case GMCode.Ret:
                case GMCode.Pushenv:
                    break;
                case GMCode.Dup:
                    throw new Exception("Need more info for dup");
                case GMCode.Neg:
                case GMCode.Not:
                case GMCode.Add:
                case GMCode.Sub:
                case GMCode.Mul:
                case GMCode.Div:
                case GMCode.Mod:
                case GMCode.And:
                case GMCode.Or:
                case GMCode.Xor:
                case GMCode.Sal:
                case GMCode.Seq:
                case GMCode.Sge:
                case GMCode.Sgt:
                case GMCode.Sle:
                case GMCode.Slt:
                case GMCode.Sne:
                    return 1;
                default:
                    throw new Exception("Unkonwn opcode");

            }
            return 0;
        }
        public static int? GetPopDelta(this Instruction i)
        {
            int count = 0;
            switch (i.Code)
            {

                case GMCode.Call:
                    count = i.Extra; // number of args
                    break;
                case GMCode.Push:
                    if (i.Types[0] == GM_Type.Var)
                    {
                        if (i.Extra == 0) count++; // the instance is on the stack
                        if ((int)i.Operand >= 0) count++; // it is an array so need the index
                    }
                    break;
                case GMCode.Pop:
                    count = 1;
                    if (i.Extra == 0) count++; // the instance is on the stack
                    if ((int)i.Operand >= 0) count++; // it is an array so need the index
                    break;
                case GMCode.Dup:
                    if (i.Extra == 0) count = 1;
                    else count = 2; // we need to figure this out
                    break;
                default:
                    count = i.Code.GetPopDelta();
                    break;
            }
            return count;
        }
        public static int GetPushDelta(this Instruction i)
        {
            switch (i.Code)
            {
                case GMCode.Dup:
                    if (i.Extra == 0) return 1;
                    else return 2; // we need to figure this out
                default:
                    return i.Code.GetPushDelta();

            }
        }
        public static List<T> CutRange<T>(this List<T> list, int start, int count)
        {
            List<T> ret = new List<T>(count);
            for (int i = 0; i < count; i++)
            {
                ret.Add(list[start + i]);
            }
            list.RemoveRange(start, count);
            return ret;
        }

        public static T[] Union<T>(this T[] a, T b)
        {
            if (a.Length == 0)
                return new[] { b };
            if (Array.IndexOf(a, b) >= 0)
                return a;
            var res = new T[a.Length + 1];
            Array.Copy(a, 0, res, 0, a.Length);
            res[res.Length - 1] = b;
            return res;
        }

        public static T[] Union<T>(this T[] a, T[] b)
        {
            if (a == b)
                return a;
            if (a.Length == 0)
                return b;
            if (b.Length == 0)
                return a;
            if (a.Length == 1)
            {
                if (b.Length == 1)
                    return a[0].Equals(b[0]) ? a : new[] { a[0], b[0] };
                return b.Union(a[0]);
            }
            if (b.Length == 1)
                return a.Union(b[0]);
            return Enumerable.Union(a, b).ToArray();
        }
    }
    public class ILAstBuilder
    {
        /// <summary> Immutable </summary>
        SortedList<int, Instruction> _method;
        List<string> InstanceList;
        List<string> StringList;
        bool optimize;

        ILExpression OperandToExpresson(object obj, GM_Type type)
        { // throws things if the cast is bad
            switch (type)
            {
                case GM_Type.Bool: return new ILExpression(GMCode.Constant, (bool)obj);
                case GM_Type.Double: return new ILExpression(GMCode.Constant, (double)obj);
                case GM_Type.Float: return new ILExpression(GMCode.Constant, (float)obj);
                case GM_Type.Long: return new ILExpression(GMCode.Constant, (long)obj);
                case GM_Type.Int: return new ILExpression(GMCode.Constant, (int)obj);
                case GM_Type.String: return new ILExpression(GMCode.Constant, (string)obj);
                case GM_Type.Short: return new ILExpression(GMCode.Constant, (int)obj);
                default:
                    throw new Exception("Cannot convert simple type");
            }
        }
        /// <summary>
        /// This just removes pushes and gets the expresion from it
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        ILExpression GetConstantExpression(ILExpression v)
        {
            if (v.Code == GMCode.Push) return v.Arguments.Single();
            else return v;
        }
        ILExpression InstanceToExpression(int instance)
        {
            if(instance < 0)
            {
                string instanceName;
                if (GMCodeUtil.instanceLookup.TryGetValue(instance, out instanceName))
                    return new ILExpression(GMCode.Constant, instanceName);
                
            } else if(InstanceList != null && instance>0 && instance < InstanceList.Count)
            {
                return new ILExpression(GMCode.Constant, InstanceList[instance]);
            }
            // fallback
            return new ILExpression(GMCode.Constant, instance);  
        }
        ILExpression InstanceToExpression(ILExpression instance)
        {
            switch (instance.Code)
            {
                case GMCode.Constant:
                    if (instance.Operand is int)
                    {
                        ILExpression ret = InstanceToExpression((int)instance.Operand);
                        ret.ILRanges = instance.ILRanges;
                        instance = ret;
                    }
                    break;
                case GMCode.Push: // it was a push, pull the arg out and try it
                    return InstanceToExpression(instance.Arguments.Single());
                case GMCode.Var:
                    break; // if its a var like global.var.something = then just pass it though
                case GMCode.Pop:
                    break; // this is filler in to be filled in latter?  yea
                default:
                    throw new Exception("Something went wrong?");
            }
            return instance;// eveything else we just return as we cannot simplify it
        }
        ILExpression BuildVar(int operand, int extra, List<ILNode> nodes)
        {
            ILExpression v = new ILExpression(GMCode.Var, StringList[operand & 0x1FFFFF]);// standard for eveyone

            // check if its simple
            if (extra != 0) // its not on the stack, so its not an array and we have the instance so resolve the name, simple
            {
                v.Arguments.Add(InstanceToExpression(extra));
            }
            else // its ON the stack so all we know is if its an array or if is
            {
                if (operand >= 0) // is array
                {
                    List<ILExpression> match;
                    if (nodes.MatchLastCount(GMCode.Push, 2, out match))
                    {
                        v.Arguments.Add(match[1]); // instance first
                        v.Arguments.Add(match[0]); // then index
                        nodes.RemoveLast(2);
                    }
                    else // put pop fillers
                    {
                        v.Arguments.Add(new ILExpression(GMCode.Pop, null)); // instance first
                        v.Arguments.Add(new ILExpression(GMCode.Pop, null)); // instance first
                    }
                }
                else
                {
                    ILExpression match;
                    if (nodes.LastOrDefault().Match(GMCode.Push, out match))
                    {
                        v.Arguments.Add(match); // instance
                        nodes.RemoveLast();
                    }
                    else
                    {
                        v.Arguments.Add(new ILExpression(GMCode.Pop, null)); // filler
                    }
                }
                v.Arguments[0] = InstanceToExpression(v.Arguments[0]); // fix instance
            }
            return v;
        }
        // This tries to do a VERY simple resolve of a var.
        // for instance, if its an array, and the index is a simple constant, remove it from nodes and asemble a proper ILVarable

        ILExpression TryResolveSimpleExpresions(ILExpression v, List<ILNode> nodes)
        {
            do
            {
                int popCount = v.Code.GetPopDelta();
                if (popCount == 0) break;
                ILExpression arg1 = popCount > 0 ? nodes.LastOrDefault() as ILExpression : null;
                ILExpression arg2 = popCount > 1 ? nodes.ElementAtOrDefault(nodes.Count - 2) as ILExpression : null; // its on a stack so reverse order
                if (arg1 != null) {
                    if (arg1.Code == GMCode.Push) arg1 = arg1.Arguments[0];
                    else if(arg1.Code != GMCode.Call) break;
                }
                if (arg2 != null)
                {
                    if (arg2.Code == GMCode.Push) arg2 = arg2.Arguments[0];
                    else if (arg2.Code != GMCode.Call) break;
                }
                v.Arguments.Clear();
                if (arg2 != null) { nodes.RemoveLast(); v.Arguments.Add(arg2); }
                if (arg1 != null) { nodes.RemoveLast(); v.Arguments.Add(arg1); }
            } while (false);
            return  new ILExpression(GMCode.Push, null, v);
        }
        ILExpression TryResolveCall(string funcName, int length, List<ILNode> nodes)
        {
            ILExpression call = new ILExpression(GMCode.Call, funcName);
            List<ILExpression> args;
            if (nodes.MatchLastCount(GMCode.Push, length, out args))
            {
                nodes.RemoveLast(length);
                call.Arguments = args;
                return call;
            }
            else
            {
                while (length-- > 0) call.Arguments.Add(new ILExpression(GMCode.Pop, null));
                return call;// fail, couldn't find constant arguments;
            }
        }
        List<ILNode> BuildPreAst()
        { // Just convert instructions to ast streight
            List<ILNode> nodes = new List<ILNode>();
            Dictionary<int, ILLabel> labels = new Dictionary<int, ILLabel>();
            Func<Label, ILLabel> ConvertLabel = (Label l) =>
             {
                 ILLabel lookup;
                 if (labels.TryGetValue(l.Address, out lookup)) return lookup;
                 lookup = new ILLabel() { Name = l.ToString(), UserData = l };
                 labels.Add(l.Address, lookup);
                 return lookup;
             };
            foreach (var i in _method.Values)
            {
                GMCode code = i.Code;
                object operand = i.Operand;
                int extra = i.Extra;
                if (i.Label != null) nodes.Add(ConvertLabel(i.Label));
                ILExpression expr = null;
                switch (code)
                {
                    case GMCode.Conv:
                        continue; // ignore all Conv for now
                    case GMCode.Call:
                        expr = new ILExpression(GMCode.Push, null, TryResolveCall(operand as string, extra, nodes));
                        break;
                    case GMCode.Popz:
                        {
                            ILExpression push;
                            if (nodes.Last().Match(GMCode.Push, out push) && push.Code == GMCode.Call)
                            {
                                nodes[nodes.Count - 1] = push;  // its not a push anymore as it was popped void statment
                                continue;
                            }
                            else expr = new ILExpression(code, null);
                        }
                        break;
                    case GMCode.Pop: // var define, so lets define it
                        expr = BuildVar((int)operand, extra,nodes);  // try to figure out the var
                        expr = new ILExpression(GMCode.Assign, null, expr); // change it to an assign
                        {
                            ILExpression push; // see if we can get the value
                            if (nodes.LastOrDefault().Match(GMCode.Push, out push) || nodes.LastOrDefault().Match(GMCode.Call, out push))
                            {
                                nodes.RemoveLast();
                                expr.Arguments.Add(push);
                            }
                        }
                        break;
                    case GMCode.Push:
                        if (i.Types[0] != GM_Type.Var)
                            expr = new ILExpression(GMCode.Push, null, OperandToExpresson(operand, i.Types[0]));// simple constant 
                        else 
                            expr = new ILExpression(GMCode.Push, null, BuildVar((int)operand, extra, nodes));  // try to figure out the var);
                        break;
                    case GMCode.Pushenv: // the asembler converted the positions to labels at the end of the push/pop enviroments
                        {
                            expr = new ILExpression(GMCode.Pushenv, ConvertLabel(i.Operand as Label));
                            ILExpression push;
                            if (nodes.LastOrDefault().Match(GMCode.Push, out push))
                            {
                                nodes.RemoveLast();
                                expr.Arguments.Add(InstanceToExpression(push));
                            } else expr.Arguments.Add(new ILExpression(GMCode.Pop, null));
                        }
                        break;
                    case GMCode.Popenv:
                        expr = new ILExpression(GMCode.Popenv, ConvertLabel(i.Operand as Label));
                        break;
                    case GMCode.B:
                    case GMCode.Bt:
                    case GMCode.Bf: // we could try converting all Bf to Bt here, but Bt's seem to only be used in special shorts or switch/case, so save that info here
                        expr = new ILExpression(code, ConvertLabel(operand as Label));
                        break;
                    case GMCode.Dup:
                        expr = new ILExpression(code, extra); // save the extra value for dups incase its dup eveything or just one
                        break;
                    case GMCode.Exit:
                        expr = new ILExpression(code, null);
                        break;
                    default:
                        expr = new ILExpression(code, null);
                        if (code.GetPopDelta() > 0) expr = TryResolveSimpleExpresions( expr, nodes);
                        break;
                }
                expr.ILRanges.Add(new ILRange(i.Address, i.Address));
                nodes.Add(expr);
            }
            return nodes;
        }
        // try to match a patern that goes with a switch and change it to a switch Expression
        // detect the size of the case going backwards, if there isn't a case there return -1;
        int FindEndOfSwitch(List<ILNode> ast, ILLabel fallOutLabel)
        {
            ILLabel test;
            for (int i = 0; i < ast.Count; i++)
            {
                if (ast[i].Match(GMCode.Bt) && ast[i + 1].Match(GMCode.B, out test) && (test == fallOutLabel)) return i + 1;
            }
            return -1;
        }
        // trying to do this here instead of the Optimize portion
        bool MatchPushConstant(List<ILNode> nodes, int start, out ILExpression expr)
        {
            do
            {
                ILExpression e = nodes.ElementAtOrDefault(start) as ILExpression;
                if (e == null) break;
                if (e.Code == GMCode.Call) expr = e;
                else if (e.Code == GMCode.Push) expr = e.Arguments[0];
                else break;
                return true;
            } while (false);
            expr = default(ILExpression);
            return false;
        }

        bool SimplifyExpression(int start, List<ILNode> nodes)
        {
            do
            {
                ILExpression expr;
                if (!nodes[start].Match(GMCode.Push, out expr) || !expr.Code.isExpression()) break;
                int popDelta = expr.Code.GetPopDelta();
                if (popDelta == 1)
                {
                    if (expr.Arguments.Count > 0 && expr.Arguments[0].Code != GMCode.Pop) break; // already resolved
                    ILExpression arg1 = null;
                    if (MatchPushConstant(nodes, start - 1, out arg1))
                    {
                        expr.Arguments.Clear();
                        expr.Arguments.Add(arg1);
                        nodes.RemoveAt(start - 1);
                    }
                    else break; // couldn't match
                }
                else if (popDelta == 2)
                {
                    if (expr.Arguments.Count > 1 && expr.Arguments[0].Code != GMCode.Pop && expr.Arguments[1].Code != GMCode.Pop) break; // already resolved

                    ILExpression arg1 = null;
                    ILExpression arg2 = null;
                    if (MatchPushConstant(nodes, start - 1, out arg2) && MatchPushConstant(nodes, start - 2, out arg1))
                    {
                        expr.Arguments.Clear();
                        expr.Arguments.Add(arg1);
                        expr.Arguments.Add(arg2);
                        nodes.RemoveRange(start - 2, 2);
                    }
                    else break; // couldn't match
                }
                return true;
            } while (false);
            return false;
        }
        public bool SwitchDetection(int start, List<ILNode> ast)
        {
            int from = start;
            do
            {
                int popz = ast.FindLastIndexOf(GMCode.Popz, from); // Only time popz is used is in switches and calls
                if (popz == -1 || ast.ElementAtOrDefault(popz - 1).Match(GMCode.Call)) break; // skip and continue
                ILLabel fallOutLabel = ast[popz - 1] as ILLabel;
                Debug.Assert(fallOutLabel != null); // Label should be right before it
                // Now that we have the fallOutLabel, lets find the patern that is the end of the long switch branches
                int endCase = FindEndOfSwitch(ast, fallOutLabel);
                int startCase = endCase - 1;
                Debug.Assert(endCase != -1);    // find the start index of the switch case
                for (int current = ast.FindLastIndexOf(GMCode.Dup, endCase - 1); current != -1; current = ast.FindLastIndexOf(GMCode.Dup, current - 1)) startCase = current;
                ILExpression switchExpression = ast[startCase - 1] as ILExpression;
                Debug.Assert(switchExpression != null); // HAS to be an expression
                Debug.Assert(switchExpression.Code == GMCode.Push);
                //switchExpression = switchExpression.Arguments.Single(); // get the condition
                List<ILNode> switchBlock = new List<ILNode>();
                for (int i = startCase; i < ((endCase + 1) - startCase); i++)
                {
                    ILExpression e = ast[i] as ILExpression;
                    Debug.Assert(e != null); // more checking, make sure we have just expressions in here
                    if (e.Code == GMCode.Dup) e = new ILExpression(switchExpression, e.ILRanges); // replace it with the switch expression
                    switchBlock.Add(e);
                }
                DoPattern(switchBlock, SimplifyExpression);
                // Time to make the switch expression!
                ILExpression finalSwitch = new ILExpression(GMCode.Switch, fallOutLabel);
                finalSwitch.Arguments.Add(switchExpression); // first argument is the compare switch condition
                for (int i = 0; i < switchBlock.Count; i += 2)
                { // evey two expresions condition/ label
                    ILLabel caseLabel;
                    ILExpression condition;
                    if (switchBlock[i].Match(GMCode.Push, out condition)
                        && switchBlock.ElementAtOrDefault(i + 1).Match(GMCode.Bt, out caseLabel))
                    {
                        Debug.Assert(condition.Code.isExpression()); // should be an expression
                        finalSwitch.Arguments.Add(new ILExpression(GMCode.Case, caseLabel, condition.Arguments[1])); // Just add the right hand
                    }
                }
                // be sure to remove from reverse order so the indexes we have are still valid
                ast.RemoveAt(popz); // finaly remove the popz at the end of the case as the stack shold be clean then
              //  ast.RemoveAt(endCase); // remove the end casegoto as we don't need it anymore

                // Now the annoying part.  we have to remove all the nodes
                ast.RemoveRange(startCase, endCase - startCase);
                // then replace the push condition with the switch statement
                ast[startCase - 1] = finalSwitch;
                return true;
            } while (false);
            return false;
        }
        public bool MatchDupPatern(int start, List<ILNode> nodes)
        {
            /* Pattern is
                Push instance
                Push arrayIndex
                Dup 1 // I THINK this copys the entire stack
                %POP%.msg[%POP%] = %POP%.msg[%POP%] + something
                I beleve this can be rolled into the assignAdd code
            */
            do
            {
                int index = start;
                IList<ILExpression> args;
                if (!nodes[index--].Match(GMCode.Assign, out args)) break; // try to match a assign first
                int dupCount;
                if (!nodes.ElementAtOrDefault(index--).Match(GMCode.Dup, out dupCount)) break;
                Debug.Assert(dupCount == 0 || dupCount == 1); // only seen these two
                ILExpression instance = null;
                ILExpression arrayIndex = null;

                if (!nodes.ElementAtOrDefault(index).Match(GMCode.Push, out instance)) break; // we need this push
                if (dupCount == 1)
                {
                    arrayIndex = instance; // first push was index
                    if (!nodes.ElementAtOrDefault(--index).Match(GMCode.Push, out instance)) break; // we need this push for index
                }
                Debug.Assert(dupCount == 1);
                instance = InstanceToExpression(instance); // try to resolve the instance
                // We got all we needed, lets check the assignment
                Debug.Assert(args[0].Code == GMCode.Var); // sanity check
                Debug.Assert((args[0].Arguments.Count == 1 && arrayIndex == null) || (args[0].Arguments.Count == 2 && arrayIndex != null));
                args[0].Arguments[0] = instance;
                if (arrayIndex != null) args[0].Arguments[1] = arrayIndex;
                // now the left hand of the expresson for assgment
                args[1].Arguments[0].Arguments[0] = instance;
                if (arrayIndex != null) args[1].Arguments[0].Arguments[1] = arrayIndex;
                // DONE! lets clean up being sure not to remove the assign we just modified
                nodes.RemoveRange(index, start - index);
                return true;
            } while (false);
            return false;
        }
        // match all function calls that ignore the return and remove the popz
        // also, to save on looping we also throw if there are any dup's left
        // this much be run at the end
        public bool FixPopZandCheckDUP(int start, List<ILNode> nodes)
        {
            do
            {
               // if (nodes[start].Match(GMCode.Dup) &&) throw new Exception("We Missed a Dup");
                if (!nodes[start].Match(GMCode.Call) 
                     || !nodes.ElementAtOrDefault(start + 1).Match(GMCode.Popz)) break;
                nodes.RemoveAt(start+1); // remove it
                return true;
            } while (false);
            return false;
        }
        // We try to resolve simple push enviroments here
        public bool SimplfyPushEnviroments(int start, List<ILNode> nodes)
        {
            /* A simple push envorment is
                // pushenv object
                // single statement, var assign or call
                // pop L393
                // L393:
                We don't want to remove the label as it might be used by other things
                This will simplify graph making as the only time the graph will care
                Is when the pop enviroment breaks
                Be sure to run all the optimziers first BEFORE this or put this in a big loop
                till eveything is fixed and optimized
            */
            do
            {
                ILExpression pushEnv = nodes[start] as ILExpression; // make sure its resolved and a push
                if (pushEnv == null || pushEnv.Code != GMCode.Pushenv || pushEnv.Arguments[0].Code == GMCode.Pop) break;
                ILExpression popEnv = nodes.ElementAtOrDefault(start+2) as ILExpression;
                ILBlock block = new ILBlock();
                int index = start+1;
                bool nope = false;
                while ((popEnv = nodes.ElementAtOrDefault(index++) as ILExpression) != null) {
                    Debug.Assert(popEnv.Code != GMCode.Pushenv); // ugh, this will be annoying if I run into it
                    if (popEnv.Code == GMCode.Popenv) break;
                    else if(popEnv.IsBranch()) { nope = true; break; }
                    else block.Body.Add(popEnv);
                }
                if (popEnv == null || nope) break; // There are labels and/or ifstatements in here
                Debug.Assert((popEnv.Operand as ILLabel) == (pushEnv.Operand as ILLabel)); // they should exit the same
                nodes[start] = new ILWithStatement() { Enviroment = pushEnv.Arguments[0], Body = block };
                int count = index - start - 1;
                nodes.RemoveRange(start + 1, count);
                return true; 
                // we will want to remove the extra label statements that arn't used as well
                // but that princess is in another castle

            } while (false);
            return false;
        }
        public void DoPattern<T>(List<T> nodes, Func<int, List<T>, bool> pred) where T : ILNode
        {
            bool modified;
            do
            {
                modified = false;
                for (int i = 0; i < nodes.Count; i++) modified |= pred(i, nodes);
            } while (modified);
        }
        void FlattenBasicBlocks(ILNode node)
        {
            ILBlock block = node as ILBlock;
            if (block != null)
            {
                List<ILNode> flatBody = new List<ILNode>();
                foreach (ILNode child in block.GetChildren())
                {
                    FlattenBasicBlocks(child);
                    ILBasicBlock childAsBB = child as ILBasicBlock;
                    if (childAsBB != null)
                    {
                        if (!(childAsBB.Body.FirstOrDefault() is ILLabel))
                            throw new Exception("Basic block has to start with a label. \n" + childAsBB.ToString());
                        if (childAsBB.Body.LastOrDefault() is ILExpression && !childAsBB.Body.LastOrDefault().IsUnconditionalControlFlow())
                            throw new Exception("Basci block has to end with unconditional control flow. \n" + childAsBB.ToString());
                        flatBody.AddRange(childAsBB.GetChildren());
                    }
                    else {
                        flatBody.Add(child);
                    }
                }
                block.EntryGoto = null;
                block.Body = flatBody;
            }
            else if (node is ILExpression)
            {
                // Optimization - no need to check expressions
            }
            else if (node != null)
            {
                // Recursively find all ILBlocks
                foreach (ILNode child in node.GetChildren())
                {
                    FlattenBasicBlocks(child);
                }
            }
        }
        public List<ILNode> Build(SortedList<int, Instruction> code, bool optimize, List<string> StringList, List<string> InstanceList = null) //DecompilerContext context)
        {
            if (code.Count == 0) return new List<ILNode>();
            //variables = new Dictionary<string, VariableDefinition>();
            this.InstanceList = InstanceList;
            this.StringList = StringList;
            _method = code;
            this.optimize = optimize;
            List<ILNode> ast = BuildPreAst();
            
         //   DoPattern(ast, FixPopZandCheckDUP);
            DoPattern(ast, SwitchDetection);
            DoPattern(ast, MatchDupPatern);
            // DoPattern(ast, SimplfyPushEnviroments);
            ILBlock method = new ILBlock();
            method.Body = ast;
            betteribttest.Dissasembler.Optimize.RemoveRedundantCode(method);
            foreach(var block in method.GetSelfAndChildrenRecursive<ILBlock>())
                Optimize.SplitToBasicBlocks(block);

            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                bool modified;
                do
                {
                    modified = false;

                    modified |= block.RunOptimization(new SimpleControlFlow(method).SimplifyShortCircuit);
                    modified |= block.RunOptimization(new SimpleControlFlow(method).JoinBasicBlocks);

                    //  modified |= block.RunOptimization(SimplifyLogicNot);
                    //  modified |= block.RunOptimization(MakeAssignmentExpression);
                } while (modified);
            }
          
            method.Body.DebugPrintILAst("before_loop.txt");
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                new LoopsAndConditions().FindLoops(block);
            }
            method.Body.DebugPrintILAst("before_conditions.txt");
            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                new LoopsAndConditions().FindConditions(block);
            }
            FlattenBasicBlocks(method);
 
            Optimize.RemoveRedundantCode(method);
            new GotoRemoval().RemoveGotos(method);
            Optimize.RemoveRedundantCode(method);
            //List<ByteCode> body = StackAnalysis(method);
            method.Body.DebugPrintILAst("bytecode_test.txt");

            // We don't have a fancy


            return ast;

        }
    }
}