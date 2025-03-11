// /******************************************************************************
//  * File: Pen.cs
//  * Copyright (c) 2023 Qualcomm Technologies, Inc. and/or its subsidiaries. All rights reserved.
//  *
//  *
//  ******************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using QCHT.Interactions.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace QCHT.Samples.Drawing
{
    /// <summary>
    /// Abstract class.
    /// Defines drawing tool (Pen)
    /// </summary>
    public abstract class Pen : MonoBehaviour
    {
        private const float GLOBAL_WIDTH_REFERENCE = 0.0015f;

        [Flags, Serializable]
        public enum DrawingMode
        {
            None = 0x0,
            LeftHand = 0x1,
            RightHand = 0x2,
        };

        /// <summary>
        /// The current drawing mode.
        /// </summary>
        [Header("Hands")]
        [SerializeField]
        protected DrawingMode _drawingMode = DrawingMode.RightHand;
        
        [Header("Pointer")]
        [SerializeField] protected InputActionProperty isLeftTracked;
        
        [SerializeField] protected PencilPointer pencilLeftPointer;

        [SerializeField] protected InputActionProperty isRightTracked;

        [SerializeField] protected PencilPointer pencilRightPointer;

        [Header("Brush")]
        [SerializeField] protected BrushDescriptor defaultBrush;

        [SerializeField] protected float defaultWidth = 1f;

        [Header("Audio")]
        [SerializeField] protected AudioSource audioSource;
        
        /// <summary>
        /// The maximum number of simultaneous points supported
        /// </summary>
        private static int MAX_POINT_LIMIT = 1500;
        
        /// <summary>
        /// The current used brush.
        /// </summary>
        protected BrushDescriptor _brush;

        /// <summary>
        /// The current brush width.
        /// </summary>
        protected float _width;

        /// <summary>
        /// Is currently drawing ?
        /// </summary>
        private bool _isLeftDrawing;

        private bool _isRightDrawing;

        /// <summary>
        /// Stores each pencil lines made by the pen tool.
        /// </summary>
        protected Queue<CurvedLineRenderer> _pencilLinesHistory = new Queue<CurvedLineRenderer>();

        /// <summary>
        /// Stores each pencil removed historical pencil lines.
        /// </summary>
        protected readonly Stack<CurvedLineRenderer> _pencilLinesRedo = new Stack<CurvedLineRenderer>();

        #region MonoBehaviour Functions

        public void Start()
        {
            SetBrush(defaultBrush);
            SetWidth(defaultWidth);
        }

        public void OnEnable()
        {
            isLeftTracked.EnableDirectAction();
            isRightTracked.EnableDirectAction();

            pencilRightPointer.onPenDown += OnPencilDown;
            pencilRightPointer.onPenUp += OnPencilUp;
            pencilLeftPointer.onPenDown += OnPencilDown;
            pencilLeftPointer.onPenUp += OnPencilUp;
        }

        public void OnDisable()
        {
            isLeftTracked.DisableDirectAction();
            isRightTracked.DisableDirectAction();
            
            pencilRightPointer.onPenDown -= OnPencilDown;
            pencilRightPointer.onPenUp -= OnPencilUp;
            pencilLeftPointer.onPenDown -= OnPencilDown;
            pencilLeftPointer.onPenUp -= OnPencilUp;
        }

        public void Update()
        {
            UpdatePencil(true, pencilLeftPointer, ref _isLeftDrawing);
            UpdatePencil(false, pencilRightPointer, ref _isRightDrawing);
            ApplyMaximumPointsLimit();
        }

        #endregion

        #region Public Functions

        /// <summary>
        /// Sets the brush descriptor for this pen.
        /// </summary>
        /// <param name="brush"> The selected brush descriptor. </param>
        public void SetBrush(BrushDescriptor brush)
        {
            _brush = brush;

            var color = _brush.Type == BrushDescriptor.ColorType.Gradient
                ? _brush.Gradient.Evaluate(1f)
                : _brush.Color;

            pencilLeftPointer.SetColor(color);
            pencilRightPointer.SetColor(color);

            if (_brush.LineParticles)
            {
                pencilLeftPointer.SetLineParticles(Instantiate(_brush.LineParticles).GetComponent<ParticleSystem>());
                pencilRightPointer.SetLineParticles(Instantiate(_brush.LineParticles).GetComponent<ParticleSystem>());
            }
            else
            {
                pencilLeftPointer.DestroyLineParticles();
                pencilRightPointer.DestroyLineParticles();
            }
        }

        /// <summary>
        /// Sets the width of the pencil.
        /// It is multiplied by the GLOBAL_WIDTH_REFERENCE in order to friendly scale it.
        /// </summary>
        /// <param name="width"> The new desired width. </param>
        public void SetWidth(float width)
        {
            _width = width * GLOBAL_WIDTH_REFERENCE;
            pencilLeftPointer.SetScale(_width);
            pencilRightPointer.SetScale(_width);
        }

        /// <summary>
        /// Sets the drawing mode.
        /// </summary>
        public void SetDrawingMode(DrawingMode drawingMode)
        {
            _drawingMode = drawingMode;
        }

        /// <summary>
        /// Undo the last line.
        /// </summary>
        public void Undo()
        {
            if (_pencilLinesHistory.Count == 0)
                return;

            var lineHistory = _pencilLinesHistory.ToList();
            var line = lineHistory.Last();

            if (!line)
                return;
            
            line.gameObject.SetActive(false);
            _pencilLinesRedo.Push(line);
        }

        /// <summary>
        /// Redo the last removed/hidden line.
        /// </summary>
        public void Redo()
        {
            if (_pencilLinesRedo.Count == 0)
                return;

            var line = _pencilLinesRedo.Pop();
            line.gameObject.SetActive(true);
            _pencilLinesHistory.Enqueue(line);
        }

        /// <summary>
        /// Clear all lines and clear the history.
        /// </summary>
        public void Clear()
        {
            foreach (var line in _pencilLinesHistory)
                Destroy(line);

            foreach (var line in _pencilLinesRedo)
                Destroy(line);

            OnPenClear();
        }

        private void ApplyMaximumPointsLimit()
        {
            var extraPointsCount = GetTotalDrawnPoints() - MAX_POINT_LIMIT;
            if (extraPointsCount <= 0)
            {
                return;
            }
            
            var oldestLine = _pencilLinesHistory.Peek();
            var oldestLinePointsCount = oldestLine.LineRenderer.positionCount;

            if (oldestLinePointsCount < extraPointsCount)
            {
                //need to delete more than a line
                Destroy(_pencilLinesHistory.Dequeue().gameObject);
                ApplyMaximumPointsLimit();
            }
            else
            {
                //need to shorten this line only
                var remainingPointCount = oldestLinePointsCount - extraPointsCount;
                var oldestLinePoints = new Vector3[oldestLinePointsCount];
                oldestLine.LineRenderer.GetPositions(oldestLinePoints);
                var cleanedLinePoints = new Vector3[remainingPointCount];
                
                for (int i = 0; i < remainingPointCount; i++)
                {
                    cleanedLinePoints[i] = oldestLinePoints[i + extraPointsCount];
                }
                oldestLine.SetPositions(cleanedLinePoints);
            }
        }

        private int GetTotalDrawnPoints()
        {
            return _pencilLinesHistory.Sum(curvedLineRenderer => curvedLineRenderer.LineRenderer.positionCount);
        }

        #endregion

        /// <summary>
        /// Performs drawing logic for a given hand.
        /// </summary>
        /// <param name="isLeft">Is left hand?</param>
        /// <param name="pencilPointer">The pencil pointer to update.</param>
        /// <param name="isDrawing">Is hand currently drawing?</param>
        private void UpdatePencil(bool isLeft, PencilPointer pencilPointer, ref bool isDrawing)
        {
            var checkActive = isLeft
                ? _drawingMode.HasFlag(DrawingMode.LeftHand)
                : _drawingMode.HasFlag(DrawingMode.RightHand);

            var isTracked = isLeft ? isLeftTracked.action.IsPressed() : isRightTracked.action.IsPressed();
            if (!checkActive || !isTracked)
            {
                pencilPointer.Hide();
                if (!isDrawing) return;
                isDrawing = false;
                OnPenUp(isLeft);
                pencilPointer.StopLineParticles();
                return;
            }

            pencilPointer.Show();
            UpdatePointer(isLeft, pencilPointer);
            
            // Drawing
            if (isDrawing)
            {
                OnPenDrawingUpdate(isLeft);
            }
            else
            {
                pencilPointer.Show();
            }
        }

        /// <summary>
        /// Updates the pointer position and scale.
        /// </summary>
        private static void UpdatePointer(bool isLeft, PencilPointer pencilPointer)
        {
            pencilPointer.UpdateScale();
        }


        private void OnPencilDown(XrHandedness handedness)
        {
            var isLeft = handedness == XrHandedness.XR_HAND_LEFT;
            ref var isDrawing = ref isLeft ? ref _isLeftDrawing : ref _isRightDrawing;
            isDrawing = true;
            OnPenDown(isLeft);

            if (!audioSource && !_brush.StartDrawing)
                audioSource.PlayOneShot(_brush.StartDrawing);
        }
        
        private void OnPencilUp(XrHandedness handedness)
        {
            var isLeft = handedness == XrHandedness.XR_HAND_LEFT;
            ref var isDrawing = ref isLeft ? ref _isLeftDrawing : ref _isRightDrawing;
            isDrawing = false;
            OnPenUp(isLeft);
        }
        
        #region Abstract Functions

        /// <summary>
        /// Called when the pen started drawing.
        /// </summary>
        protected abstract void OnPenDown(bool isLeft);

        /// <summary>
        /// Called when the pen stopped drawing.
        /// </summary>
        protected abstract void OnPenUp(bool isLeft);

        /// <summary>
        /// Called each frame when the pen currently drawing.
        /// </summary>
        protected abstract void OnPenDrawingUpdate(bool isLeft);

        /// <summary>
        /// Called when pen was cleared.
        /// </summary>
        protected abstract void OnPenClear();
        
        #endregion
    }
}