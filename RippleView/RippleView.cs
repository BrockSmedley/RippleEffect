/*
* The MIT License (MIT)
*
* Copyright (c) 2014 Robin Chutaux
* Copyright (c) 2014 Tomasz Cielecki
*
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
*
* The above copyright notice and this permission notice shall be included in
* all copies or substantial portions of the Software.
*
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
* THE SOFTWARE.
*/

using System;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;

namespace Cheesebaron.RippleEffect
{
    public class RippleView 
        : RelativeLayout
    {
        private int _width;
        private int _height;
        private int _frameRate = 10;
        private int _duration = 400;
        private int _paintAlpha = 90;
        private Handler _canvasHandler;
        private float _radiusMax;
        private bool _animationRunning;
        private int _timer;
        private int _timerEmpty;
        private int _durationEmpty = -1;
        private float _x = -1;
        private float _y = -1;
        private Animation _scaleAnimation;
        private bool _hasToZoom;
        private bool _isCentered;
        private int _rippleType;
        private Paint _paint;
        private Bitmap _originBitmap;
        private Color _rippleColor;

        protected RippleView(IntPtr javaReference, JniHandleOwnership transfer) 
            : base(javaReference, transfer) { }

        public RippleView(Context context) 
            : base(context) { }

        public RippleView(Context context, IAttributeSet attrs) 
            : this(context, attrs, 0) { }

        public RippleView(Context context, IAttributeSet attrs, int defStyle) 
            : base(context, attrs, defStyle)
        {
            Init(context, attrs);
        }

        private void Init(Context context, IAttributeSet attrs)
        {
            var a = context.ObtainStyledAttributes(attrs, Resource.Styleable.RippleView);
            _rippleColor = a.GetColor(Resource.Styleable.RippleView_rvColor,
                Resources.GetColor(Resource.Color.__rippleViewDefaultColor));
            _rippleType = a.GetInt(Resource.Styleable.RippleView_rvType, 0);
            _hasToZoom = a.GetBoolean(Resource.Styleable.RippleView_rvZoom, false);
            _isCentered = a.GetBoolean(Resource.Styleable.RippleView_rvCentered, false);
            _duration = a.GetInt(Resource.Styleable.RippleView_rvRippleDuration, _duration);
            _frameRate = a.GetInt(Resource.Styleable.RippleView_rvFramerate, _frameRate);
            _paintAlpha = a.GetInt(Resource.Styleable.RippleView_rvAlpha, _paintAlpha);
            _canvasHandler = new Handler();
            _scaleAnimation = AnimationUtils.LoadAnimation(context, Resource.Animation.zoom);
            _scaleAnimation.Duration = a.GetInt(Resource.Styleable.RippleView_rvZoomDuration, 150);
            _paint = new Paint(PaintFlags.AntiAlias);
            _paint.SetStyle(Paint.Style.Fill);
            _paint.Color = _rippleColor;
            _paint.Alpha = _paintAlpha;

            a.Recycle();

            SetWillNotDraw(false);
            DrawingCacheEnabled = true;
        }

        public override void Draw(Canvas canvas)
        {
            base.Draw(canvas);
            if (!_animationRunning) return;

            if (_duration <= _timer * _frameRate)
            {
                _animationRunning = false;
                _timer = 0;
                _durationEmpty = -1;
                _timerEmpty = 0;
                canvas.Restore();
                Invalidate();
                return;
            }
            
            _canvasHandler.PostDelayed(Invalidate, _frameRate);

            if (_timer == 0)
                canvas.Save();

            canvas.DrawCircle(_x, _y, (_radiusMax * (((float) _timer * _frameRate) / _duration)), _paint);

            _paint.Color = Resources.GetColor(Android.Resource.Color.HoloRedLight);

            if (_rippleType == 1 && _originBitmap != null && (((float) _timer * _frameRate) / _duration) > 0.4f)
            {
                if (_durationEmpty == -1)
                    _durationEmpty = _duration - _timer * _frameRate;

                _timerEmpty++;
                using (var tmpBitmap = GetCircleBitmap((int) (_paintAlpha - ((_paintAlpha) * 
                        (((float) _timerEmpty * _frameRate) / (_durationEmpty))))))
                {
                    canvas.DrawBitmap(tmpBitmap, 0, 0, _paint);
                    tmpBitmap.Recycle();
                }
            }

            _paint.Color = _rippleColor;

            _timer++;

            if (_rippleType == 1)
            {
                if ((((float) _timer * _frameRate) / _duration) > 0.6f)
                    _paint.Alpha =
                        (int) (_paintAlpha - (_paintAlpha * (((float) _timerEmpty * _frameRate) / _durationEmpty)));
                else
                    _paint.Alpha = _paintAlpha;
            }
            else
                _paint.Alpha = (int)(_paintAlpha - (_paintAlpha * (((float)_timerEmpty * _frameRate) / _duration)));
        }

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            _width = MeasureSpec.GetSize(widthMeasureSpec);
            _height = MeasureSpec.GetSize(heightMeasureSpec);
            SetMeasuredDimension(_width, _height);
            base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
        }

        public override bool OnInterceptTouchEvent(MotionEvent ev)
        {
            if (_animationRunning)
                return base.OnInterceptTouchEvent(ev);

            if (_hasToZoom)
                StartAnimation(_scaleAnimation);

            _radiusMax = Math.Max(_width, _height) / 2f;
            if (_isCentered || _rippleType == 1)
            {
                _x = MeasuredHeight / 2;
                _y = MeasuredWidth / 2;
            }
            else
            {
                _x = ev.GetX();
                _y = ev.GetY();
            }
            _animationRunning = true;

            if (_rippleType == 1 && _originBitmap == null)
                _originBitmap = GetDrawingCache(true);

            Invalidate();

            return base.OnInterceptTouchEvent(ev);
        }

        private Bitmap GetCircleBitmap(int radius)
        {
            var output = Bitmap.CreateBitmap(_originBitmap.Width, _originBitmap.Height, Bitmap.Config.Argb8888);
            using (var canvas = new Canvas(output))
            using (var paint = new Paint(PaintFlags.AntiAlias))
            using (var rect = new Rect((int)(_x - radius), (int)(_y - radius), (int)(_x + radius), (int)(_y + radius)))
            {
                canvas.DrawARGB(0, 0, 0, 0);
                canvas.DrawCircle(_x, _y, radius, paint);

                paint.SetXfermode(new PorterDuffXfermode(PorterDuff.Mode.SrcIn));
                canvas.DrawBitmap(_originBitmap, rect, rect, paint);

                return output;
            }
        }
    }
}