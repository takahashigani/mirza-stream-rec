// /******************************************************************************
//  * File: XRDirectInteractorFilter.cs
//  * Copyright (c) 2023 Qualcomm Technologies, Inc. and/or its subsidiaries. All rights reserved.
//  *
//  *
//  ******************************************************************************/

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

#if XRIT_3_0_0_OR_NEWER
using UnityEngine.XR.Interaction.Toolkit.Interactors;
#endif

namespace QCHT.Interactions.Distal
{
    public class XRDirectInteractorFilter : MonoBehaviour, IXRRayInteractorFilter
    {
        [SerializeField] public XRDirectInteractor directInteractor;
         
        public bool CanShowRay => directInteractor == null || !directInteractor.hasHover;
    }
}