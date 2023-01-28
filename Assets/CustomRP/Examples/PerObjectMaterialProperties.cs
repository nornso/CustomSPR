﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour
{
    readonly int baseColorId = Shader.PropertyToID("_BaseColor"),
               cutoffID = Shader.PropertyToID("_Cutoff"),
               metallicId = Shader.PropertyToID("_Metallic"),
               smoothnessId = Shader.PropertyToID("_Smoothness");

    static MaterialPropertyBlock block;

    [SerializeField]
    Color baseColor = Color.white;

    [SerializeField, Range(0f, 1f)]
    float alphaCutoff = 0.5f, metallic = 0f, smoothness = 0.5f;

    private void Awake()
    {
        OnValidate();
    }

    void OnValidate()
    {
        if (block == null)
            block = new MaterialPropertyBlock();

        block.SetColor(baseColorId, baseColor);
        block.SetFloat(cutoffID, alphaCutoff);
        block.SetFloat(metallicId, metallic);
        block.SetFloat(smoothnessId, smoothness);
        GetComponent<Renderer>().SetPropertyBlock(block);
    }
}
