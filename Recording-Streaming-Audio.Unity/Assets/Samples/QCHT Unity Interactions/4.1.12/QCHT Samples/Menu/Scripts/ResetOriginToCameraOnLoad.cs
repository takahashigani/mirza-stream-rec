// /******************************************************************************
//  * File: ResetOriginToCameraOnLoad.cs
//  * Copyright (c) 2023 Qualcomm Technologies, Inc. and/or its subsidiaries. All rights reserved.
//  *
//  *
//  ******************************************************************************/

using QCHT.Interactions.Core;
using UnityEngine;

namespace QCHT.Samples.Menu
{
    public class ResetOriginToCameraOnLoad : MonoBehaviour
    {
        public bool ResetSessionOriginOnStart = true;

        private bool _isSessionOriginMoved;

        private void OnEnable() => OffsetSessionOrigin();

        private void Update()
        {
            var cameraInOriginSpaces = Vector3.zero;
            var isSet = false;

            if (XROriginUtility.FindXROrigin() is var xrOrigin && xrOrigin != null)
            {
                cameraInOriginSpaces = xrOrigin.CameraInOriginSpacePos;
                isSet = true;
            }
            
            if (ResetSessionOriginOnStart && !_isSessionOriginMoved && cameraInOriginSpaces != Vector3.zero && isSet)
            {
                OffsetSessionOrigin();
                _isSessionOriginMoved = true;
            }
        }

        public void Recenter()
        {
            _isSessionOriginMoved = false;
        }

#pragma warning disable CS0219 // Variable is assigned but its value is never used
        private void OffsetSessionOrigin()
        {
            Transform sessionOrigin = null;
            Transform cameraTransform = null;
            var isSet = false;
            
            if (XROriginUtility.FindXROrigin() is var xrOrigin && xrOrigin != null)
            {
                sessionOrigin = xrOrigin.Origin.transform;
                cameraTransform = xrOrigin.Camera.transform;
                isSet = true;
            }
            
            if (sessionOrigin != null && cameraTransform != null)
            {
                var t = sessionOrigin.transform;
                t.Rotate(0.0f, -cameraTransform.rotation.eulerAngles.y, 0.0f, Space.World);
                t.position -= cameraTransform.position;
            }
        }
#pragma warning restore CS0219 // Variable is assigned but its value is never used
    }
}