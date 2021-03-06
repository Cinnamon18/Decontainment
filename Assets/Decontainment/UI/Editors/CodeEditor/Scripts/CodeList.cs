﻿using Asm;
using Bot;
using Extensions;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Editor.Code
{
    public class CodeList : MonoBehaviour
    {
        [SerializeField]
        private GameObject instructionBlockPrefab = null;
        [SerializeField]
        private GameObject labelBlockPrefab = null;
        [SerializeField]
        private GameObject dividerPrefab = null;
        [SerializeField]
        private RectTransform viewportRT = null;
        [SerializeField]
        private ScrollRect scrollRect = null;
        [SerializeField]
        private Tab tab = null;

        private bool initialCGBlocksRaycasts;
        private Transform[] instructionBlockTransforms;

        private Program _program;
        private List<Draggable.Slot> _dividers = new List<Draggable.Slot>();
        private List<Draggable.Slot> _slotFields = new List<Draggable.Slot>();
        private List<Draggable.Slot> _trashSlots = new List<Draggable.Slot>();
        private List<Block> _blocks = new List<Block>();

        private SelectionManager selectionManager;

        private Canvas canvas;
        private CanvasGroup cg;
        private ContentSizeFitter csf;
        private RectTransform rt;

        public event Action OnScroll;
        public event Action<Program> OnProgramChange;

        public Transform[] InstructionBlockTransforms { get { return instructionBlockTransforms; } }

        public Program Program
        {
            get { return _program; }
            set {
                Program oldProgram = _program;
                _program = value;

                if (oldProgram != null) {
                    oldProgram.OnChange -= HandleProgramChange;
                }

                Clear();
                if (_program != null) {
                    _program.OnChange += HandleProgramChange;
                    Generate();
                }

                OnProgramChange?.Invoke(oldProgram);
            }
        }
        public List<Draggable.Slot> Dividers => _dividers;
        public List<Draggable.Slot> SlotFields => _slotFields;
        public List<Draggable.Slot> TrashSlots => _trashSlots;
        public List<Block> Blocks => _blocks;
        public SelectionManager SelectionManager => selectionManager;

        void OnEnable()
        {
            scrollRect.onValueChanged.AddListener(HandleScroll);
        }

        void OnDisable()
        {
            scrollRect.onValueChanged.RemoveListener(HandleScroll);
        }

        void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            cg = GetComponent<CanvasGroup>();
            csf = GetComponent<ContentSizeFitter>();
            rt = GetComponent<RectTransform>();
            selectionManager = GetComponent<SelectionManager>();

            initialCGBlocksRaycasts = cg.blocksRaycasts;
        }

        void Update()
        {
            if (!tab.IsFocused) {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Delete) && selectionManager.RegionSelected) {
                selectionManager.DeleteSelection();
                Program.BroadcastMultiChange(new Program.Change(){ instruction = true, branchLabel = true });
            }

            if (Input.GetKeyDown(KeyCode.X) && Input.GetKey(KeyCode.LeftControl) && selectionManager.RegionSelected) {
                selectionManager.CopySelection();
                selectionManager.DeleteSelection();
                PromptSystem.Instance.PromptOtherAction("Selection cut");
                Program.BroadcastMultiChange(new Program.Change(){ instruction = true, branchLabel = true });
            }

            if (Input.GetKeyDown(KeyCode.C) && Input.GetKey(KeyCode.LeftControl) && selectionManager.RegionSelected) {
                selectionManager.CopySelection();
                PromptSystem.Instance.PromptOtherAction("Selection copied");
            }

            if (Input.GetKeyDown(KeyCode.V) && Input.GetKey(KeyCode.LeftControl)) {
                if (selectionManager.Pastable) {
                    selectionManager.PasteBuffer();
                    Program.BroadcastMultiChange(new Program.Change(){ instruction = true, branchLabel = true });
                } else {
                    PromptSystem.Instance.PromptInvalidAction("No selection to paste on");
                }
            }
        }

        // Ensure that code blocks are always at least as wide as the viewport
        void OnRectTransformDimensionsChange()
        {
            if (instructionBlockTransforms == null || Program == null) {
                return;
            }

            float maxInstructionBlockWidth = 0;
            foreach (Transform instructionBlock in instructionBlockTransforms) {
                RectTransform instructionBlockRT = instructionBlock.GetComponent<RectTransform>();
                maxInstructionBlockWidth = Mathf.Max(instructionBlockRT.GetWorldSize().x, maxInstructionBlockWidth);
            }

            float viewportWidth = viewportRT.GetWorldSize().x;
            if (viewportWidth < maxInstructionBlockWidth && !Mathf.Approximately(viewportWidth, maxInstructionBlockWidth)) {
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            } else {
                csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                rt.sizeDelta = new Vector2(0, rt.sizeDelta.y);
            }
        }



        public void Reset()
        {
            Clear();
            Generate();
        }

        private void HandleProgramChange(Program.Change change)
        {
            if (change.instruction || change.branchLabel) {
                Reset();
            }
        }

        private void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; --i) {
                Destroy(transform.GetChild(i).gameObject);
            }

            Dividers.Clear();
            SlotFields.Clear();
            Blocks.Clear();
            selectionManager.Reset();
        }

        private void Generate()
        {
            cg.blocksRaycasts = initialCGBlocksRaycasts;

            // Create code block for each instruction in program
            instructionBlockTransforms = new Transform[Program.instructions.Count];
            int lineNumber = 0;
            int nextLabelIndex = 0;
            foreach (Instruction instruction in Program.instructions) {
                // Create any labels for the current line
                while (nextLabelIndex < Program.branchLabelList.Count && Program.branchLabelList[nextLabelIndex].val == lineNumber) {
                    CreateLabel(ref nextLabelIndex);
                }

                // Create divider
                Divider divider = Instantiate(dividerPrefab, transform, false).GetComponent<Divider>();
                divider.Init(lineNumber, selectionManager);
                Dividers.Add(divider);

                // Create instruction block
                GameObject instructionBlockGO = Instantiate(instructionBlockPrefab, transform, false);
                InstructionBlock instructionBlock = instructionBlockGO.GetComponent<InstructionBlock>();
                instructionBlock.Init(lineNumber, instruction, divider, this);

                Blocks.Add(instructionBlock);
                instructionBlockTransforms[lineNumber] = instructionBlockGO.transform;
                ++lineNumber;
            }

            // Create any remaining labels
            while (nextLabelIndex < Program.branchLabelList.Count) {
                CreateLabel(ref nextLabelIndex);
            }

            // Create end divider
            Divider endDivider = Instantiate(dividerPrefab, transform, false).GetComponent<Divider>();
            endDivider.Init(lineNumber, selectionManager);
            Dividers.Add(endDivider);

            OnRectTransformDimensionsChange();
        }

        private void CreateLabel(ref int nextLabelIndex)
        {
            Label label = Program.branchLabelList[nextLabelIndex];

            Divider labelDivider = Instantiate(dividerPrefab, transform, false).GetComponent<Divider>();
            labelDivider.Init(label.val, selectionManager, label);

            Dividers.Add(labelDivider);

            // TODO: Need to test on bad devices to see if there's a performance hit when the code list is reset
            // NOTE: Maybe if there is, we can use pooling and continue to do a full reset
            GameObject labelBlockGO = Instantiate(labelBlockPrefab, transform, false);
            LabelBlock labelBlock = labelBlockGO.GetComponent<LabelBlock>();
            labelBlock.Init(label, labelDivider, this);
            Blocks.Add(labelBlock);
            ++nextLabelIndex;
        }

        private void HandleScroll(Vector2 pos)
        {
            OnScroll?.Invoke();
        }
    }
}
