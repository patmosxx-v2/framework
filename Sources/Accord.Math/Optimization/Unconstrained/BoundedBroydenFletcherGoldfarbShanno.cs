﻿// Accord Math Library
// The Accord.NET Framework
// http://accord-framework.net
//
// Copyright © César Souza, 2009-2014
// cesarsouza at gmail.com
//
// Copyright © Jorge Nocedal, 1990
// http://users.eecs.northwestern.edu/~nocedal/
//
//    This library is free software; you can redistribute it and/or
//    modify it under the terms of the GNU Lesser General Public
//    License as published by the Free Software Foundation; either
//    version 2.1 of the License, or (at your option) any later version.
//
//    This library is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
//    Lesser General Public License for more details.
//
//    You should have received a copy of the GNU Lesser General Public
//    License along with this library; if not, write to the Free Software
//    Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
//

namespace Accord.Math.Optimization
{
    using System;

    /// <summary>
    ///   Limited-memory Broyden–Fletcher–Goldfarb–Shanno (L-BFGS) optimization method.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>
    ///   The L-BFGS algorithm is a member of the broad family of quasi-Newton optimization
    ///   methods. L-BFGS stands for 'Limited memory BFGS'. Indeed, L-BFGS uses a limited
    ///   memory variation of the Broyden–Fletcher–Goldfarb–Shanno (BFGS) update to approximate
    ///   the inverse Hessian matrix (denoted by Hk). Unlike the original BFGS method which
    ///   stores a dense  approximation, L-BFGS stores only a few vectors that represent the
    ///   approximation implicitly. Due to its moderate memory requirement, L-BFGS method is
    ///   particularly well suited for optimization problems with a large number of variables.</para>
    /// <para>
    ///   L-BFGS never explicitly forms or stores Hk. Instead, it maintains a history of the past
    ///   <c>m</c> updates of the position <c>x</c> and gradient <c>g</c>, where generally the history
    ///   <c>m</c>can be short, often less than 10. These updates are used to implicitly do operations
    ///   requiring the Hk-vector product.</para>
    ///   
    /// <para>
    ///   The framework implementation of this method is based on the original FORTRAN source code
    ///   by Jorge Nocedal (see references below). The original FORTRAN source code of L-BFGS (for
    ///   unconstrained problems) is available at http://www.netlib.org/opt/lbfgs_um.shar and had
    ///   been made available under the public domain. </para>
    /// 
    /// <para>
    ///   References:
    ///   <list type="bullet">
    ///     <item><description><a href="http://www.netlib.org/opt/lbfgs_um.shar">
    ///        Jorge Nocedal. Limited memory BFGS method for large scale optimization (Fortran source code). 1990.
    ///        Available in http://www.netlib.org/opt/lbfgs_um.shar </a></description></item>
    ///     <item><description>
    ///        Jorge Nocedal. Updating Quasi-Newton Matrices with Limited Storage. <i>Mathematics of Computation</i>,
    ///        Vol. 35, No. 151, pp. 773--782, 1980.</description></item>
    ///     <item><description>
    ///        Dong C. Liu, Jorge Nocedal. On the limited memory BFGS method for large scale optimization.</description></item>
    ///    </list></para>
    /// </remarks>
    /// 
    /// <example>
    /// <para>
    ///   The following example shows the basic usage of the L-BFGS solver
    ///   to find the minimum of a function specifying its function and
    ///   gradient. </para>
    ///   
    /// <code>
    /// // Suppose we would like to find the minimum of the function
    /// // 
    /// //   f(x,y)  =  -exp{-(x-1)²} - exp{-(y-2)²/2}
    /// //
    /// 
    /// // First we need write down the function either as a named
    /// // method, an anonymous method or as a lambda function:
    /// 
    /// Func&lt;double[], double> f = (x) =>
    ///     -Math.Exp(-Math.Pow(x[0] - 1, 2)) - Math.Exp(-0.5 * Math.Pow(x[1] - 2, 2));
    /// 
    /// // Now, we need to write its gradient, which is just the
    /// // vector of first partial derivatives del_f / del_x, as:
    /// //
    /// //   g(x,y)  =  { del f / del x, del f / del y }
    /// // 
    /// 
    /// Func&lt;double[], double[]> g = (x) => new double[] 
    /// {
    ///     // df/dx = {-2 e^(-    (x-1)^2) (x-1)}
    ///     2 * Math.Exp(-Math.Pow(x[0] - 1, 2)) * (x[0] - 1),
    /// 
    ///     // df/dy = {-  e^(-1/2 (y-2)^2) (y-2)}
    ///     Math.Exp(-0.5 * Math.Pow(x[1] - 2, 2)) * (x[1] - 2)
    /// };
    /// 
    /// // Finally, we can create the L-BFGS solver, passing the functions as arguments
    /// var lbfgs = new BroydenFletcherGoldfarbShanno(numberOfVariables: 2, function: f, gradient: g);
    /// 
    /// // And then minimize the function:
    /// double minValue = lbfgs.Minimize();
    /// double[] solution = lbfgs.Solution;
    /// 
    /// // The resultant minimum value should be -2, and the solution
    /// // vector should be { 1.0, 2.0 }. The answer can be checked on
    /// // Wolfram Alpha by clicking the following the link:
    /// 
    /// // http://www.wolframalpha.com/input/?i=maximize+%28exp%28-%28x-1%29%C2%B2%29+%2B+exp%28-%28y-2%29%C2%B2%2F2%29%29
    /// 
    /// </code>
    /// </example>
    /// 
    public class BoundedBroydenFletcherGoldfarbShanno : IGradientOptimizationMethod
    {
        // those values need not be modified
        private const double stpmin = 1e-20;
        private const double stpmax = 1e20;

        // Line search parameters
        private int maxfev = 20;

        private int iterations;
        private int evaluations;

        private int numberOfVariables;
        private int corrections = 5;

        private double[] x; // current solution x
        private double f;   // value at current solution f(x)
        double[] g;         // gradient at current solution

        private Func<double[], double[]> gradient;

        private double[] lowerBound;
        private double[] upperBound;

        private double[] work;

        // 
        // c     We specify the tolerances in the stopping criteria.
        // 
        double factr = 1e+5; // factr: 1.d+12 for
        // c         low accuracy; 1.d+7 for moderate accuracy; 1.d+1 for extremely
        // c         high accuracy.
        double pgtol = 0.0;//1.0e-5;


        #region Properties

        /// <summary>
        ///   Occurs when progress is made during the optimization.
        /// </summary>
        /// 
        public event EventHandler<OptimizationProgressEventArgs> Progress;

        /// <summary>
        ///   Gets or sets the function to be optimized.
        /// </summary>
        /// 
        /// <value>The function to be optimized.</value>
        /// 
        public Func<double[], double> Function { get; set; }

        /// <summary>
        ///   Gets or sets a function returning the gradient
        ///   vector of the function to be optimized for a
        ///   given value of its free parameters.
        /// </summary>
        /// 
        /// <value>The gradient function.</value>
        /// 
        public Func<double[], double[]> Gradient { get; set; }

        /// <summary>
        ///   Gets the number of variables (free parameters)
        ///   in the optimization problem.
        /// </summary>
        /// 
        /// <value>The number of parameters.</value>
        /// 
        public int Parameters
        {
            get { return numberOfVariables; }
        }

        /// <summary>
        ///   Gets the number of iterations performed in the last
        ///   call to <see cref="Minimize()"/>.
        /// </summary>
        /// 
        /// <value>
        ///   The number of iterations performed
        ///   in the previous optimization.</value>
        ///   
        public int Iterations
        {
            get { return iterations; }
        }

        /// <summary>
        ///   Gets or sets the maximum number of iterations
        ///   to be performed during optimization. Default
        ///   is 0 (iterate until convergence).
        /// </summary>
        /// 
        public int MaxIterations
        {
            get;
            set;
        }

        /// <summary>
        ///   Gets the number of function evaluations performed
        ///   in the last call to <see cref="Minimize()"/>.
        /// </summary>
        /// 
        /// <value>
        ///   The number of evaluations performed
        ///   in the previous optimization.</value>
        ///   
        public int Evaluations
        {
            get { return evaluations; }
        }

        /// <summary>
        ///   Gets or sets the number of corrections used in the L-BFGS
        ///   update. Recommended values are between 3 and 7. Default is 5.
        /// </summary>
        /// 
        public int Corrections
        {
            get { return corrections; }
            set
            {
                if (value <= 0)
                    throw exception("value", "ERROR: M .LE. 0");

                corrections = value;
            }
        }

        /// <summary>
        ///   Gets or sets the upper bounds of the interval
        ///   in which the solution must be found.
        /// </summary>
        /// 
        public double[] UpperBounds
        {
            get { return upperBound; }
        }

        /// <summary>
        ///   Gets or sets the lower bounds of the interval
        ///   in which the solution must be found.
        /// </summary>
        /// 
        public double[] LowerBounds
        {
            get { return lowerBound; }
        }


        public double Tolerance
        {
            get { return factr; }
            set {
                if (value < 0)
                    throw exception("Tolerance must be greater than or equal to zero.", "ERROR: FACTR .LT. 0");
                
                factr = value; }
        }


        public double Precision
        {
            get { return pgtol; }
            set { pgtol = value; }
        }

        /// <summary>
        ///   Gets the solution found, the values of the
        ///   parameters which optimizes the function.
        /// </summary>
        /// 
        public double[] Solution
        {
            get { return x; }
        }

        /// <summary>
        ///   Gets the output of the function at the current solution.
        /// </summary>
        /// 
        public double Value
        {
            get { return f; }
        }

        public enum Code
        {
            Success,
            ABNORMAL_TERMINATION_IN_LNSRCH,
            Convergence,
            ConvergenceGradient
        }

        public Code Status { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        ///   Creates a new instance of the L-BFGS optimization algorithm.
        /// </summary>
        /// 
        /// <param name="numberOfVariables">The number of free parameters in the optimization problem.</param>
        /// 
        public BoundedBroydenFletcherGoldfarbShanno(int numberOfVariables)
        {
            if (numberOfVariables <= 0)
                throw new ArgumentOutOfRangeException("numberOfVariables");

            this.numberOfVariables = numberOfVariables;

            this.upperBound = new double[numberOfVariables];
            this.lowerBound = new double[numberOfVariables];

            for (int i = 0; i < upperBound.Length; i++)
                lowerBound[i] = Double.NegativeInfinity;

            for (int i = 0; i < upperBound.Length; i++)
                upperBound[i] = Double.PositiveInfinity;

            x = new double[numberOfVariables];
        }

        /// <summary>
        ///   Creates a new instance of the L-BFGS optimization algorithm.
        /// </summary>
        /// 
        /// <param name="numberOfVariables">The number of free parameters in the function to be optimized.</param>
        /// <param name="function">The function to be optimized.</param>
        /// <param name="gradient">The gradient of the function.</param>
        /// 
        public BoundedBroydenFletcherGoldfarbShanno(int numberOfVariables,
            Func<double[], double> function, Func<double[], double[]> gradient)
            : this(numberOfVariables)
        {
            if (function == null)
                throw new ArgumentNullException("function");

            if (gradient == null)
                throw new ArgumentNullException("gradient");

            this.Function = function;
            this.Gradient = gradient;

        }

        #endregion

        /// <summary>
        ///   Minimizes the defined function. 
        /// </summary>
        /// 
        /// <param name="values">The initial guess values for the parameters. Default is the zero vector.</param>
        /// 
        /// <returns>The minimum value found at the <see cref="Solution"/>.</returns>
        /// 
        public double Minimize()
        {
            return Minimize(Solution);
        }

        private void probeGradient(Func<double[], double[]> value)
        {
            double[] probe = new double[numberOfVariables];
            double[] result = value(probe);

            if (result == probe)
                throw new ArgumentException();
            if (probe.Length != result.Length)
                throw new ArgumentException();

            for (int i = 0; i < probe.Length; i++)
            {
                if (probe[i] != 0.0)
                    throw new ArgumentException();
            }
        }

        /// <summary>
        ///   Minimizes the defined function. 
        /// </summary>
        /// 
        /// <param name="values">The initial guess values for the parameters. Default is the zero vector.</param>
        /// 
        /// <returns>The minimum value found at the <see cref="Solution"/>.</returns>
        /// 
        public double Minimize(double[] values)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            if (values.Length != numberOfVariables)
                throw new DimensionMismatchException("values");

            if (Function == null)
                throw new ArgumentNullException("function");

            if (Gradient == null)
                throw new ArgumentNullException("gradient");

            probeGradient(Gradient);

            for (int j = 0; j < Solution.Length; j++)
            {
                Solution[j] = values[j];
            }

            int n = numberOfVariables;
            int m = corrections;

            String task = "";
            String csave = "";
            bool[] lsave = new bool[4];
            int iprint = 101;
            int[] nbd = new int[n];
            int[] iwa = new int[3 * n];
            int[] isave = new int[60];
            double f = 0.0d;
            double[] x = new double[n];
            double[] l = new double[n];
            double[] u = new double[n];
            double[] g = new double[n];
            double[] dsave = new double[60];
            int totalSize = 2 * m * n + 11 * m * m + 5 * n + 8 * m;
            double[] wa = new double[totalSize];

            int i = 0;

            {
                for (i = 0; i < UpperBounds.Length; i++)
                {
                    bool hasUpper = !Double.IsInfinity(UpperBounds[i]);
                    bool hasLower = !Double.IsInfinity(LowerBounds[i]);

                    if (hasUpper && hasLower)
                        nbd[i] = 2;
                    else if (hasUpper)
                        nbd[i] = 3;
                    else if (hasLower)
                        nbd[i] = 1;
                    else nbd[i] = 0; // unbounded

                    if (hasLower)
                        l[i] = LowerBounds[i];
                    if (hasUpper)
                        u[i] = UpperBounds[i];
                }
            }


            // We now define the starting point.
            {
                for (i = 0; i < n; i++)
                    x[i] = values[i];
            }


            // We start the iteration by initializing task.
            task = "START";

        // 
        // c        ------- the beginning of the loop ----------
        // 
        L111:
            // 
            // c     This is the call to the L-BFGS-B code.
            // 
            Setulb.setulb(n, m, x, 0, l, 0, u, 0, nbd, 0, ref f, g, 0,
                factr, pgtol, wa, 0, iwa, 0, ref task, iprint, ref csave,
                lsave, 0, isave, 0, dsave, 0);

        if (Progress != null)
            Progress(this, new OptimizationProgressEventArgs(0,0,null,0,null,0,0,0,false)
            {
                Tag = Tuple.Create((int[])isave.Clone(), (double[])dsave.Clone())
            });

            // 
            if ((task.StartsWith("FG")))
            {
                // c        the minimization routine has returned to request the
                // c        function f and gradient g values at the current x.
                // 
                // c        Compute function value f for the sample problem.
                // 
                f = Function(x.Submatrix(n));

                // 
                // c        Compute gradient g for the sample problem.
                // 
                double[] newG = Gradient(x.Submatrix(n));
                for (int j = 0; j < newG.Length; j++)
                    g[j] = newG[j];

                // 
                // c          go back to the minimization routine.
                goto L111;
            }

            // c
            else if ((task.StartsWith("NEW_X")))
            {
                goto L111;
            }
            else
            {
                if (task == "ABNORMAL_TERMINATION_IN_LNSRCH")
                    Status = Code.ABNORMAL_TERMINATION_IN_LNSRCH;
                else if (task == "CONVERGENCE: REL_REDUCTION_OF_F_<=_FACTR*EPSMCH")
                    Status = Code.Convergence;
                else if (task == "CONVERGENCE: NORM_OF_PROJECTED_GRADIENT_<=_PGTOL")
                    Status = Code.ConvergenceGradient;
                else throw exception(task, task);
                // return Double.NaN;
            }

            // c        the minimization routine has returned with a new iterate,
            // c         and we have opted to continue the iteration.
            // 
            // c           ---------- the end of the loop -------------
            // 
            // c     If task is neither FG nor NEW_X we terminate execution.
            // 

            for (int j = 0; j < Solution.Length; j++)
            {
                Solution[j] = x[j];
            }

            return f;
        }


        private static ArgumentOutOfRangeException exception(string message, string code,
          string paramName = null)
        {
            if (paramName == null)
                paramName = "value";

            var e = new ArgumentOutOfRangeException(paramName, message);
            e.Data["Code"] = code;
            return e;
        }

        private static InvalidOperationException operationException(string message, string code)
        {
            var e = new InvalidOperationException(message);
            e.Data["Code"] = code;
            return e;
        }

    }
}