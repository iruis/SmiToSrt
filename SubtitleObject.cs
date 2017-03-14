using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace SmiToSrt
{
    public class SubtitleObject : DependencyObject
    {
        public int Index
        {
            get { return (int)GetValue(IndexProperty); }
            set { SetValue(IndexProperty, value); }
        }

        public static readonly DependencyProperty IndexProperty =
            DependencyProperty.Register("Index", typeof(int), typeof(SubtitleObject), new PropertyMetadata(-1));

        public TimeSpan Start
        {
            get { return (TimeSpan)GetValue(StartProperty); }
            set { SetValue(StartProperty, value); }
        }

        public static readonly DependencyProperty StartProperty =
            DependencyProperty.Register("Start", typeof(TimeSpan), typeof(SubtitleObject), new PropertyMetadata(TimeSpan.MinValue));

        public TimeSpan Stop
        {
            get { return (TimeSpan)GetValue(StopProperty); }
            set { SetValue(StopProperty, value); }
        }

        public static readonly DependencyProperty StopProperty =
            DependencyProperty.Register("Stop", typeof(TimeSpan), typeof(SubtitleObject), new PropertyMetadata(TimeSpan.MinValue));

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(SubtitleObject), new PropertyMetadata(String.Empty));
    }
}
