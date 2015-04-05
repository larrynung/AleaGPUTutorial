﻿using System;
using Alea.CUDA;
using Alea.CUDA.IL;
using Microsoft.FSharp.Core;
using Tutorial.Fs.examples.genericReduce;

namespace Tutorial.Cs.examples.generic_reduce
{
    //using InitFunc64 = Func<Unit, double>;
    //using ReductionOp64 = Func<double, double, double>;
    //using TransformFunc64 = Func<double, double>;

    //using UpsweepKernel = Action<deviceptr<double>, deviceptr<int>, deviceptr<double>>;
    //using ReduceKernel = Action<int, deviceptr<double>>;

    public static class ReduceApi
    {
        public static Plan Plan32 = new Plan()
        {
            NumThreads = 1024,
            ValuesPerThread = 4,
            NumThreadsReduction = 256,
            BlockPerSm = 1
        };

        public static Plan Plan64 = new Plan()
        {
            NumThreads = 512,
            ValuesPerThread = 4,
            NumThreadsReduction = 256,
            BlockPerSm = 1
        };

        public class Reduce<T> : ReduceModule<T>
        {
            //private Func<Unit, T> _initFunc;
            //private Func<T,T,T> _reductionOp;
            //private Func<T, T> _transform;
            //private readonly ReduceModule<T> _reduce; 
            //private Plan _plan;

            public Reduce(GPUModuleTarget target, Func<Unit, T> initFunc, Func<T,T,T> reductionOp, Func<T,T> transform, Plan plan) 
                : base(target, initFunc, reductionOp, transform, plan)
            {
                //_initFunc = initFunc;
                //_reductionOp = reductionOp;
                //_transform = transform;
                //_reduce = new ReduceModule<T>(target, initFunc, reductionOp, transform, _plan);
            }

            public T Apply(T[] values)
            {
                var n = values.Length;
                var numSm = GPUWorker.Device.Attributes.MULTIPROCESSOR_COUNT;
                var tup = _plan.BlockRanges(numSm, n);
                var ranges = tup.Item1;
                var numRanges = tup.Item2;
                var lpUpsweep = new LaunchParam(numRanges, _plan.NumThreads);
                var lpReduce = new LaunchParam(1, _plan.NumThreadsReduction);
                
                using(var dValues = GPUWorker.Malloc(values))
                using(var dRanges = GPUWorker.Malloc(ranges))
                using (var dRangeTotals = GPUWorker.Malloc<T>(numRanges))
                {
                    // Launch range reduction kernel to calculate the totals per range.
                    GPUWorker.Launch(Upsweep, lpUpsweep, dValues.Ptr, dRanges.Ptr, dRangeTotals.Ptr);
                    if (numRanges > 1)
                    {
                        // Need to aggregate the block sums as well.
                        GPUWorker.Launch(ReduceRangeTotals, lpReduce, numRanges, dRangeTotals.Ptr);
                    }
                    return dRangeTotals.Gather()[0];
                }

            }


        }

        public static double Sum(double[] values)
        {
            return
                (new Reduce<double>(
                    GPUModuleTarget.DefaultWorker, 
                    x => 0.0, 
                    (x, y) => x + y, 
                    x => x,
                    Plan64)
                ).Apply(values);
        }

        public static float Sum(float[] values)
        {
            return
                (new Reduce<float>(
                    GPUModuleTarget.DefaultWorker,
                    x => 0.0f,
                    (x, y) => x + y,
                    x => x,
                    Plan32)
                ).Apply(values);            
        }

        
    }
}
