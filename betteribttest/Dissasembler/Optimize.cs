﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using betteribttest.GMAst;
using betteribttest.FlowAnalysis;
using System.Diagnostics;

namespace betteribttest.Dissasembler
{
    class Optimize
    {
        int nextLabelIndex = 0;
        /// <summary>
        /// Group input into a set of blocks that can be later arbitraliby schufled.
        /// The method adds necessary branches to make control flow between blocks
        /// explicit and thus order independent.
        /// </summary>

        void SplitToBasicBlocks(ILBlock block)
        {
            List<ILNode> basicBlocks = new List<ILNode>();

            ILLabel entryLabel = block.Body.FirstOrDefault() as ILLabel ?? new ILLabel() { Name = "Block_" + (nextLabelIndex++) };
            ILBasicBlock basicBlock = new ILBasicBlock();
            basicBlocks.Add(basicBlock);
            basicBlock.Body.Add(entryLabel);
            block.EntryGoto = new ILExpression(GMCode.B, entryLabel);

            if (block.Body.Count > 0)
            {
                if (block.Body[0] != entryLabel)
                    basicBlock.Body.Add(block.Body[0]);

                for (int i = 1; i < block.Body.Count; i++)
                {
                    ILNode lastNode = block.Body[i - 1];
                    ILNode currNode = block.Body[i];

                    // Start a new basic block if necessary
                    if (currNode is ILLabel ||
                        lastNode.IsConditionalControlFlow() ||
                        lastNode.IsUnconditionalControlFlow())
                    {
                        // Try to reuse the label
                        ILLabel label = currNode as ILLabel ?? new ILLabel() { Name = "Block_" + (nextLabelIndex++).ToString() };

                        // Terminate the last block
                        if (!lastNode.IsUnconditionalControlFlow())
                        {
                            // Explicit branch from one block to other
                            basicBlock.Body.Add(new ILExpression(GMCode.B, label));
                        }

                        // Start the new block
                        basicBlock = new ILBasicBlock();
                        basicBlocks.Add(basicBlock);
                        basicBlock.Body.Add(label);

                        // Add the node to the basic block
                        if (currNode != label)
                            basicBlock.Body.Add(currNode);
                    }
                    else {
                        basicBlock.Body.Add(currNode);
                    }
                }
            }

            block.Body = basicBlocks;
            return;
        }
          /// <summary>
		/// Flattens all nested basic blocks, except the the top level 'node' argument
		/// </summary>
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
        /// <summary>
		/// Removes redundatant Br, Nop, Dup, Pop
		/// Ignore arguments of 'leave'
		/// </summary>
		/// <param name="method"></param>
		internal static void RemoveRedundantCode(ILBlock method)
        {
            Dictionary<ILLabel, int> labelRefCount = new Dictionary<ILLabel, int>();
            foreach (ILLabel target in method.GetSelfAndChildrenRecursive<ILExpression>(e => e.IsBranch()).SelectMany(e => e.GetBranchTargets()))
            {
                labelRefCount[target] = labelRefCount.GetOrDefault(target) + 1;
            }

            foreach (ILBlock block in method.GetSelfAndChildrenRecursive<ILBlock>())
            {
                IList<ILNode> body = block.Body;
                List<ILNode> newBody = new List<ILNode>(body.Count);
                for (int i = 0; i < body.Count; i++)
                {
                    ILLabel target;
                    if (body[i].Match(GMCode.B, out target) && i + 1 < body.Count && body[i + 1] == target)
                    {
                        // Ignore the branch
                        if (labelRefCount[target] == 1)
                            i++;  // Ignore the label as well
                    }
                    else if (body[i].Match(GMCode.BadOp))
                    {
                    }
                    else {
                        ILLabel label = body[i] as ILLabel;
                        if (label != null)
                        {
                            if (labelRefCount.GetOrDefault(label) > 0)
                                newBody.Add(label);
                        }
                        else {
                            newBody.Add(body[i]);
                        }
                    }
                }
                block.Body = newBody;
            }


            // 'dup' removal
            foreach (ILExpression expr in method.GetSelfAndChildrenRecursive<ILExpression>())
            {
                for (int i = 0; i < expr.Arguments.Count; i++)
                {
                    ILExpression child;
                    if (expr.Arguments[i].Match(GMCode.Dup, out child))
                    {
                        child.ILRanges.AddRange(expr.Arguments[i].ILRanges);
                        expr.Arguments[i] = child;
                    }
                }
            }
        }
        #region SimplifyLogicNot
        static bool SimplifyLogicNot(List<ILNode> body, ILExpression expr, int pos)
        {
            bool modified = false;
            expr = SimplifyLogicNot(expr, ref modified);
            Debug.Assert(expr == null);
            return modified;
        }
        static bool SimplifyLogicNot(ref ILExpression expr)
        {
            bool modified = false;
            var newExpr = SimplifyLogicNot(expr, ref modified);
            if (newExpr != null) expr = newExpr;
            return modified;
        }
        static void FixAllIfStatements(ILBlock expr)
        {
            bool modified=false;
            do
            {
                modified = false;
                var list = expr.GetSelfAndChildrenRecursive<ILCondition>().ToList();
                foreach (var ifs in list) modified |= SimplifyLogicNot(ref ifs.Condition);
            } while (modified);
            do
            {
                modified = false;
                var list = expr.GetSelfAndChildrenRecursive<ILWhileLoop>().ToList();
                foreach (var loop in list) modified |= SimplifyLogicNot(ref loop.Condition);
            } while (modified);
          //  return;
            do
            {
                modified = false;
              
                // combine ifstatements to logic ands
                List<ILCondition> test = expr.GetSelfAndChildrenRecursive<ILCondition>(x => x.FalseBlock == null && x.TrueBlock.Body.Count == 1 && x.TrueBlock.Body[0] is ILCondition).ToList();
                if (test == null || test.Count > 0) modified = true;
                foreach (var ifs in test)
                {
                    var leftIf = (ifs.TrueBlock.Body[0] as ILCondition);
                    ifs.TrueBlock = leftIf.TrueBlock;
                    ifs.Condition = new ILExpression(GMCode.LogicAnd, null, ifs.Condition, leftIf.Condition);
                }
            }
            while (modified);
        }
        // This will negate a condition and optimize it
        static ILExpression NegateCondition(ILExpression expr)
        {
            Debug.Assert(expr.Code != GMCode.Push); // We don't handle pushes
            switch (expr.Code)
            {
                case GMCode.Not:
                    return expr.Arguments.Single(); // VERY simple, remove the negate
                case GMCode.Constant:
                case GMCode.Var:
                case GMCode.Call:
                    return new ILExpression(GMCode.Not, null, expr); // VERY simple, add a not

                case GMCode.Seq: expr.Code = GMCode.Sne; return expr;
                case GMCode.Sne: expr.Code = GMCode.Seq; return expr;
                case GMCode.Sgt: expr.Code = GMCode.Sle; return expr;
                case GMCode.Sge: expr.Code = GMCode.Slt; return expr;
                case GMCode.Slt: expr.Code = GMCode.Sge; return expr;
                case GMCode.Sle: expr.Code = GMCode.Sgt; return expr;
                // this is complcated as we have to negate the left and right side too
                case GMCode.LogicAnd:
                case GMCode.LogicOr:
                    expr.Code = expr.Code == GMCode.LogicOr ? GMCode.LogicAnd : GMCode.LogicOr;
                    expr.Arguments[0] = NegateCondition(expr.Arguments[0]);
                    expr.Arguments[1] = NegateCondition(expr.Arguments[1]);
                    return expr;
                case GMCode.Neg:
                    throw new Exception("Error, cannot logic negate a neg");
                default:
                    throw new Exception("Error, cannot logic negate a this code");
            }
        }
            // Tis will simplify negates that get out of control
            static ILExpression SimplifyLogicNot(ILExpression expr, ref bool modified)
        {
            ILExpression a;
#if false
            // not sure we need this
            // "ceq(a, ldc.i4.0)" becomes "logicnot(a)" if the inferred type for expression "a" is boolean
            if (expr.Code == GMCode.Seq && TypeAnalysis.IsBoolean(expr.Arguments[0].InferredType) && (a = expr.Arguments[1]).Code == ILCode.Ldc_I4 && (int)a.Operand == 0)
            {
                expr.Code = ILCode.LogicNot;
                expr.ILRanges.AddRange(a.ILRanges);
                expr.Arguments.RemoveAt(1);
                modified = true;
            }
#endif
            ILExpression res = null;
            while (expr.Code == GMCode.Not)
            {
                a = expr.Arguments[0];
                // remove double negation
                if (a.Code == GMCode.Not)
                {
                    res = a.Arguments[0];
                    res.ILRanges.AddRange(expr.ILRanges);
                    res.ILRanges.AddRange(a.ILRanges);
                    expr = res;
                }
                else {
                    if (SimplifyLogicLogicArguments(expr)) res = expr = a;
                    break;
                }
            }

            for (int i = 0; i < expr.Arguments.Count; i++)
            {
                a = SimplifyLogicNot(expr.Arguments[i], ref modified);
                if (a != null)
                {
                    expr.Arguments[i] = a;
                    modified = true;
                }
            }
           // Debug.Assert(res != null);
            return res;
        }
        static bool SimplifyLogicLogicArguments(ILExpression expr)
        {
            var a = expr.Arguments[0];
          
            switch (a.Code)
            {
                case GMCode.LogicAnd: // this is complcated
                case GMCode.LogicOr: // this is complcated
                    a.Code = a.Code == GMCode.LogicOr ? GMCode.LogicAnd : GMCode.LogicOr;
                    if (!SimplifyLogicNotArgument(a.Arguments[0])) a.Arguments[0] = new ILExpression(GMCode.Not, null, a.Arguments[0]);
                    if (!SimplifyLogicNotArgument(a.Arguments[1])) a.Arguments[1] = new ILExpression(GMCode.Not, null, a.Arguments[1]);
                    a.ILRanges.AddRange(expr.ILRanges);
                    return true;
                default:
                    return SimplifyLogicNotArgument(expr);
            }      
        }
        /// <summary>
        /// If the argument is a binary comparison operation then the negation is pushed through it
        /// </summary>
        static bool SimplifyLogicNotArgument(ILExpression expr)
        {
            var a = expr.Arguments[0];
            GMCode c;
            switch (a.Code)
            {
                case GMCode.Seq: c = GMCode.Sne; break;
                case GMCode.Sne: c = GMCode.Seq; break;
                case GMCode.Sgt: c = GMCode.Sle; break;
                case GMCode.Sge: c = GMCode.Slt; break;
                case GMCode.Slt: c = GMCode.Sge; break;
                case GMCode.Sle: c = GMCode.Sgt; break;
                default: return false;
            }
            a.Code = c;
            a.ILRanges.AddRange(expr.ILRanges);
            return true;
        }
        #endregion
    }
}