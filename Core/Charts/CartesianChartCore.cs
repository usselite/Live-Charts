﻿//The MIT License(MIT)

//copyright(c) 2016 Alberto Rodriguez

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

using System;
using System.Linq;
using LiveCharts.Definitions.Charts;
using LiveCharts.Definitions.Series;
using LiveCharts.Dtos;
using LiveCharts.Helpers;

namespace LiveCharts.Charts
{
    public class CartesianChartCore : ChartCore
    {
        #region Constructors

        public CartesianChartCore(IChartView view, ChartUpdater updater) : base(view, updater)
        {
            updater.Chart = this;
        }

        #endregion

        #region Publics

        public override void PrepareAxes()
        {
            base.PrepareAxes();

            if (View.ActualSeries.Any(x => !(x.Model is ICartesianSeries)))
                throw new LiveChartsException(
                    "There is a invalid series in the series collection, " +
                    "verify that all the series implement ICartesianSeries.");

            var cartesianSeries = View.ActualSeries.Select(x => x.Model).Cast<ICartesianSeries>().ToArray();

            for (var index = 0; index < AxisX.Count; index++)
            {
                var xi = AxisX[index];

                xi.CalculateSeparator(this, AxisOrientation.X);
                xi.BotLimit = xi.MinValue ?? cartesianSeries.Where(x => x.View.ScalesXAt == index)
                    .Select(x => x.GetMinX(xi))
                    .DefaultIfEmpty(0).Min();
                xi.TopLimit = xi.MaxValue ?? cartesianSeries.Where(x => x.View.ScalesXAt == index)
                    .Select(x => x.GetMaxX(xi))
                    .DefaultIfEmpty(0).Max();

                if (Math.Abs(xi.BotLimit - xi.TopLimit) < xi.S * .01)
                {
                    if (Math.Abs(xi.PreviousBot - xi.PreviousTop) < xi.S*.01)
                    {
                        if (xi.MinValue == null) xi.BotLimit -= xi.S;
                        else xi.BotLimit = xi.MinValue.Value;

                        if (xi.MaxValue == null) xi.TopLimit += xi.S;
                        else xi.TopLimit = xi.MaxValue.Value;

                        if (Math.Abs(xi.BotLimit - xi.TopLimit) < xi.S*.01 && !View.IsInDesignMode)
                            throw new LiveChartsException("One axis has an invalid range, it is or is really " +
                                                          "close to zero, please ensure your axis has a valid " +
                                                          "range");
                    }
                    else
                    {
                        xi.BotLimit = xi.PreviousBot;
                        xi.TopLimit = xi.PreviousTop;
                    }
                }
                xi.PreviousBot = xi.BotLimit;
                xi.PreviousTop = xi.TopLimit;
            }

            for (var index = 0; index < AxisY.Count; index++)
            {
                var yi = AxisY[index];

                yi.CalculateSeparator(this, AxisOrientation.Y);
                yi.BotLimit = yi.MinValue ?? cartesianSeries.Where(x => x.View.ScalesYAt == index)
                    .Select(x => x.GetMinY(yi))
                    .DefaultIfEmpty(0).Min();
                yi.TopLimit = yi.MaxValue ?? cartesianSeries.Where(x => x.View.ScalesYAt == index)
                    .Select(x => x.GetMaxY(yi))
                    .DefaultIfEmpty(0).Max();

                if (Math.Abs(yi.BotLimit - yi.TopLimit) < yi.S * .01)
                {
                    if (Math.Abs(yi.PreviousBot - yi.PreviousTop) < yi.S*.01)
                    {
                        if (yi.MinValue == null) yi.BotLimit -= yi.S;
                        else yi.BotLimit = yi.MinValue.Value;

                        if (yi.MaxValue == null) yi.TopLimit += yi.S;
                        else yi.TopLimit = yi.MaxValue.Value;

                        if (Math.Abs(yi.BotLimit - yi.TopLimit) < yi.S*.01)
                            throw new LiveChartsException("One axis has an invalid range, it is or is really " +
                                                          "close to zero, please ensure your axis has a valid " +
                                                          "range");
                    }
                    else
                    {
                        yi.BotLimit = yi.PreviousBot;
                        yi.TopLimit = yi.PreviousTop;
                    }
                }
                yi.PreviousBot = yi.BotLimit;
                yi.PreviousTop = yi.TopLimit;
            }

            PrepareSeries();
            CalculateComponentsAndMargin();

            for (var index = 0; index < AxisX.Count; index++)
            {
                var xi = AxisX[index];
                foreach (var section in xi.Sections)
                {
                    section.View.DrawOrMove(AxisOrientation.X, index);
                }
            }

            for (var index = 0; index < AxisY.Count; index++)
            {
                var yi = AxisY[index];
                foreach (var section in yi.Sections)
                {
                    section.View.DrawOrMove(AxisOrientation.Y, index);
                }
            }
        }

        public override void RunSpecializedChartComponents()
        {
            foreach (var visualElement in ((ICartesianChart) View).VisualElements)
            {
                visualElement.AddOrMove(this);
            }
        }

        #endregion

        #region Privates

        private void PrepareSeries()
        {
            PrepareUnitWidth();
            PrepareWeight();
            PrepareStackedColumns();
            PrepareStackedRows();
            PrepareStackedAreas();
            PrepareVerticalStackedAreas();
        }

        private void PrepareWeight()
        {
            if (!View.ActualSeries.Any(x => x is IScatterSeriesView || x is IHeatSeriesView)) return;

            var vs = View.ActualSeries
                .Select(x => x.ActualValues.GetTracker(x).WLimit)
                .DefaultIfEmpty(new CoreLimit()).ToArray();
            WLimit = new CoreLimit(vs.Select(x => x.Min).DefaultIfEmpty(0).Min(),
                vs.Select(x => x.Max).DefaultIfEmpty(0).Max());
        }

        private void PrepareUnitWidth()
        {
            foreach (var series in View.ActualSeries)
            {
                if (series is IStackedColumnSeriesView || series is IColumnSeriesView || 
                    series is IFinancialSeriesView || series is IHeatSeriesView)
                {
                    AxisX[series.ScalesXAt].EvaluatesUnitWidth = true;
                }
                if (series is IStackedRowSeriesView || series is IRowSeriesView || series is IHeatSeriesView)
                {
                    AxisY[series.ScalesYAt].EvaluatesUnitWidth = true;
                }
            }
        }

        private void PrepareStackedColumns()
        {
            if (!View.ActualSeries.Any(x => x is IStackedColumnSeriesView)) return;

            var isPercentage =
                View.ActualSeries.Any(x => x is IStackModelableSeriesView && x is IStackedColumnSeriesView &&
                                           ((IStackModelableSeriesView) x).StackMode == StackMode.Percentage);

            foreach (var group in View.ActualSeries.OfType<IStackedColumnSeriesView>().GroupBy(x => x.ScalesYAt))
            {
                StackPoints(group, AxisOrientation.Y, group.Key, isPercentage
                    ? StackMode.Percentage : StackMode.Values);
            }
        }

        private void PrepareStackedRows()
        {
            if (!View.ActualSeries.Any(x => x is IStackedRowSeriesView)) return;

            var isPercentage =
                View.ActualSeries.Any(x => x is IStackModelableSeriesView && x is IStackedRowSeriesView &&
                                     ((IStackModelableSeriesView) x).StackMode == StackMode.Percentage);

            foreach (var group in View.ActualSeries.OfType<IStackedRowSeriesView>().GroupBy(x => x.ScalesXAt))
            {
                StackPoints(group, AxisOrientation.X, group.Key, isPercentage ? StackMode.Percentage : StackMode.Values);
            }
        }

        private void PrepareStackedAreas()
        {
            if (!View.ActualSeries.Any(x => x is IStackedAreaSeriesView)) return;

            var isPercentage =
                View.ActualSeries.Any(x => x is IStackModelableSeriesView && x is IStackedAreaSeriesView &&
                                     ((IStackModelableSeriesView) x).StackMode == StackMode.Percentage);

            foreach (var group in View.ActualSeries.OfType<IStackedAreaSeriesView>().GroupBy(x => x.ScalesYAt))
            {
                StackPoints(group, AxisOrientation.Y, group.Key, isPercentage ? StackMode.Percentage : StackMode.Values);
            }
        }

        private void PrepareVerticalStackedAreas()
        {
            if (!View.ActualSeries.Any(x => x is IVerticalStackedAreaSeriesView)) return;

            var isPercentage =
                View.ActualSeries.Any(x => x is IStackModelableSeriesView && x is IVerticalStackedAreaSeriesView &&
                                     ((IStackModelableSeriesView) x).StackMode == StackMode.Percentage);

            foreach (var group in View.ActualSeries.OfType<IVerticalStackedAreaSeriesView>().GroupBy(x => x.ScalesXAt))
            {
                StackPoints(group, AxisOrientation.X, group.Key, isPercentage ? StackMode.Percentage : StackMode.Values);
            }
        }

        #endregion
    }
}
