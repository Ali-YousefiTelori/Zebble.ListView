﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Olive;

namespace Zebble
{
    partial class CollectionView<TSource>
    {
        ConcurrentDictionary<int, Range<float>> ItemPositionOffsets;
        ConcurrentDictionary<Type, Measurement> TemplateMeasuresCache = new();
        ConcurrentDictionary<Type, Task<View>> TemplateRendering = new();
        bool IsCreatingItem;

        protected virtual async Task MeasureOffsets(Guid layoutVersion)
        {
            var numProcs = Environment.ProcessorCount;
            var concurrencyLevel = numProcs * 2;
            var newOffsets = new ConcurrentDictionary<int, Range<float>>(concurrencyLevel, OnSource(x => x.Count()));
            
            var counter = 0;
             
            var from = Horizontal ? Padding.Left() : Padding.Top();

            foreach (var item in OnSource(x => x.ToArray()))
            {
                var measure = await Measure(item);
                if (layoutVersion != LayoutVersion) return;

                if (counter == 0) from += measure.Margin;

                newOffsets[counter] = new Range<float>(from, from + measure.Size);
                from += measure.Size;
                counter++;
            }
            ItemPositionOffsets = newOffsets;
        }

        async Task<Measurement> Measure(TSource item)
        {
            var actual = ViewItems().FirstOrDefault(x => x.GetViewModelValue() == item);
            if (actual != null)
            {
                return new Measurement(Direction, actual);
            }
            else
            {
                var viewType = GetViewType(item);

                if (TemplateMeasuresCache.TryGetValue(viewType, out var result))
                    return result;
                else
                {
                    var rendering = TemplateRendering.GetOrAdd(viewType, async t =>
                    {
                        try
                        {
                            IsCreatingItem = true;
                            return await Add(CreateItemView(item));
                        }
                        finally
                        {
                            IsCreatingItem = false;
                        }
                    });
                    return TemplateMeasuresCache[viewType] = new Measurement(Direction, await rendering);
                }
            }
        }

        void ResizeToEmptyTemplate()
        {
            if (Horizontal) Width.Set(FindEmptyTemplate()?.ActualWidth ?? 0);
            else Height.Set(FindEmptyTemplate()?.ActualHeight ?? 0);
        }
    }
}