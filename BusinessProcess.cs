﻿using ImageMagick;
using Org.Reddragonit.BpmEngine.Elements;
using Org.Reddragonit.BpmEngine.Elements.Processes;
using Org.Reddragonit.BpmEngine.Elements.Processes.Events;
using Org.Reddragonit.BpmEngine.Elements.Processes.Gateways;
using Org.Reddragonit.BpmEngine.Elements.Processes.Tasks;
using Org.Reddragonit.BpmEngine.Interfaces;
using Org.Reddragonit.BpmEngine.Wrappers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Xml;

namespace Org.Reddragonit.BpmEngine
{
    public sealed class BusinessProcess
    {
        private const int _ANIMATION_DELAY = 100;
        private const int _DEFAULT_PADDING = 100;
        private const int _VARIABLE_NAME_WIDTH = 200;
        private const int _VARIABLE_VALUE_WIDTH = 300;
        private const int _VARIABLE_IMAGE_WIDTH = _VARIABLE_NAME_WIDTH+_VARIABLE_VALUE_WIDTH;

        private List<object> _components;
        private List<IElement> _Elements
        {
            get
            {
                List<IElement> ret = new List<IElement>();
                foreach (object obj in _components)
                {
                    if (new List<Type>(obj.GetType().GetInterfaces()).Contains(typeof(IElement)))
                        ret.Add((IElement)obj);
                }
                return ret;
            }
        }
        private List<IElement> _FullElements
        {
            get
            {
                List<IElement> ret = new List<IElement>();
                foreach (IElement elem in _Elements){
                    _RecurAddChildren(elem, ref ret);
                }
                return ret;
            }
        }

        private void _RecurAddChildren(IElement parent, ref List<IElement> elems)
        {
            elems.Add(parent);
            if (parent is IParentElement)
            {
                foreach (IElement elem in ((IParentElement)parent).Children)
                    _RecurAddChildren(elem, ref elems);
            }
        }

        private XmlDocument _doc;
        public XmlDocument Document { get { return _doc; } }

        private ProcessState _state;
        public ProcessState State { get { return _state; } }

        #region delegates
        #region Ons
        private OnEventStarted _onEventStarted;
        public OnEventStarted OnEventStarted { get { return _onEventStarted; } set { _onEventStarted = value; } }

        private OnEventCompleted _onEventCompleted;
        public OnEventCompleted OnEventCompleted{get{return _onEventCompleted;}set{_onEventCompleted = value;}}

        private OnEventError _onEventError;
        public OnEventError OnEventError{get{return _onEventError;}set{_onEventError=value;}}

        private OnTaskStarted _onTaskStarted;
        public OnTaskStarted OnTaskStarted{get{return _onTaskStarted;}set{_onTaskStarted=value;}}

        private OnTaskCompleted _onTaskCompleted;
        public OnTaskCompleted OnTaskCompleted{get{return _onTaskCompleted;}set{_onTaskCompleted=value;}}
        
        private OnTaskError _onTaskError;
        public OnTaskError OnTaskError{get{return _onTaskError;}set{_onTaskError = value;}}

        private OnProcessStarted _onProcessStarted;
        public OnProcessStarted OnProcessStarted{get{return _onProcessStarted;}set{_onProcessStarted=value;}}
        
        private OnProcessCompleted _onProcessCompleted;
        public OnProcessCompleted OnProcessCompleted{get{return _onProcessCompleted;}set{_onProcessCompleted = value;}}

        private OnProcessError _onProcessError;
        public OnProcessError OnProcessError { get { return _onProcessError; } set { _onProcessError = value; } }

        private OnSequenceFlowCompleted _onSequenceFlowCompleted;
        public OnSequenceFlowCompleted OnSequenceFlowCompleted { get { return _onSequenceFlowCompleted; } set { _onSequenceFlowCompleted = value; } }

        private OnGatewayStarted _onGatewayStarted;
        public OnGatewayStarted OnGatewayStarted { get { return _onGatewayStarted; } set { _onGatewayStarted = value; } }

        private OnGatewayCompleted _onGatewayCompleted;
        public OnGatewayCompleted OnGatewayCompleted { get { return _onGatewayCompleted; } set { _onGatewayCompleted = value; } }

        private OnGatewayError _onGatewayError;
        public OnGatewayError OnGatewayError { get { return _onGatewayError; } set { _onGatewayError = value; } }
        #endregion

        #region Validations
        private IsEventStartValid _isEventStartValid;
        public IsEventStartValid IsEventStartValid { get { return _isEventStartValid; } set { _isEventStartValid = value; } }

        private IsProcessStartValid _isProcessStartValid;
        public IsProcessStartValid IsProcessStartValid { get { return _isProcessStartValid; } set { _isProcessStartValid = value; } }
        #endregion

        #region Conditions
        private IsFlowValid _isFlowValid;
        public IsFlowValid IsFlowValid { get { return _isFlowValid; } set { _isFlowValid = value; } }
        #endregion

        #region Tasks
        private ProcessBusinessRuleTask _processBusinessRuleTask;
        public ProcessBusinessRuleTask ProcessBusinessRuleTask { get { return _processBusinessRuleTask; } set { _processBusinessRuleTask = value; } }

        private BeginManualTask _beginManualTask;
        public BeginManualTask BeginManualTask { get { return _beginManualTask; } set { _beginManualTask = value; } }

        private ProcessRecieveTask _processRecieveTask;
        public ProcessRecieveTask ProcessRecieveTask { get { return _processRecieveTask; } set { _processRecieveTask = value; } }

        private ProcessScriptTask _processScriptTask;
        public ProcessScriptTask ProcessScriptTask { get { return _processScriptTask; } set { _processScriptTask=value; } }

        private ProcessSendTask _processSendTask;
        public ProcessSendTask ProcessSendTask { get { return _processSendTask; } set { _processSendTask = value; } }

        private ProcessServiceTask _processServiceTask;
        public ProcessServiceTask ProcessServiceTask { get { return _processServiceTask; } set { _processServiceTask = value; } }

        private ProcessTask _processTask;
        public ProcessTask ProcessTask { get { return _processTask; } set { _processTask = value; } }

        private BeginUserTask _beginUserTask;
        public BeginUserTask BeginUserTask { get { return _beginUserTask; } set { _beginUserTask = value; } }

        #region TaskCallBacks
        private void _CompleteExternalTask(string taskID, ProcessVariablesContainer variables)
        {
            bool success = false;
            foreach (IElement elem in _FullElements)
            {
                if (elem.id == taskID && elem is ATask)
                {
                    _MergeVariables((ATask)elem, variables);
                    success = true;
                    break;
                }
            }
            if (!success)
                throw new Exception(string.Format("Unable to locate task with id {0}", taskID));
        }

        private void _ErrorExternalTask(string taskID, Exception ex)
        {
            bool success = false;
            foreach (IElement elem in _FullElements)
            {
                if (elem.id == taskID && elem is ATask)
                {
                    if (_onTaskError != null)
                        _onTaskError((ATask)elem);
                    lock (_state)
                    {
                        _state.Path.FailTask((ATask)elem);
                    }
                    break;
                }
            }
            if (!success)
                throw new Exception(string.Format("Unable to locate task with id {0}", taskID));
        }

        #endregion

        #endregion

        #endregion

        private BusinessProcess() { }

        public BusinessProcess(XmlDocument doc)
        {
            _doc = doc;
            _state = new ProcessState(new ProcessStepComplete(_ProcessStepComplete),new ProcessStepError(_ProcessStepError));
            _components = new List<object>();
            foreach (XmlNode n in doc.ChildNodes)
            {
                if (n.NodeType == XmlNodeType.Element)
                {
                    Type t = Utility.LocateElementType(n.Name);
                    if (t != null)
                        _components.Add(t.GetConstructor(new Type[] { typeof(XmlElement) }).Invoke(new object[] { (XmlElement)n }));
                    else
                        _components.Add(n);
                }
                else
                    _components.Add(n);
            }
            if (_Elements.Count == 0)
                throw new XmlException("Unable to load a bussiness process from the supplied document.  No instance of bpmn:definitions was located.");
            else
            {
                bool found = false;
                foreach (IElement elem in _Elements)
                {
                    if (elem is Definition)
                        found = true;
                }
                if (!found)
                    throw new XmlException("Unable to load a bussiness process from the supplied document.  No instance of bpmn:definitions was located.");
            }
        }

        public bool LoadState(XmlDocument doc)
        {
            return _state.Load(doc);
        }

        public Bitmap Diagram(bool outputVariables)
        {
            int width = 0;
            int height = 0;
            foreach (IElement elem in _Elements)
            {
                if (elem is Definition)
                {
                    foreach (Diagram d in ((Definition)elem).Diagrams)
                    {
                        Size s = d.Size;
                        width = Math.Max(width, s.Width + _DEFAULT_PADDING);
                        height += _DEFAULT_PADDING + s.Height;
                    }
                }
            }
            Bitmap ret = new Bitmap(width, height);
            Graphics gp = Graphics.FromImage(ret);
            gp.FillRectangle(Brushes.White, new Rectangle(0, 0, width, height));
            int padding = _DEFAULT_PADDING / 2;
            foreach (IElement elem in _Elements)
            {
                if (elem is Definition)
                {
                    foreach (Diagram d in ((Definition)elem).Diagrams)
                    {
                        gp.DrawImage(d.Render(_state.Path, ((Definition)elem)), new Point(_DEFAULT_PADDING / 2, padding));
                        padding += d.Size.Height + _DEFAULT_PADDING;
                    }
                }
            }
            if (outputVariables)
            {
                SizeF sz = gp.MeasureString("Variables", Constants.FONT);
                int varHeight = (int)sz.Height+2;
                string[] keys = _state[null];
                foreach (string str in keys)
                    varHeight += (int)gp.MeasureString(str, Constants.FONT).Height + 2;
                Bitmap vmap = new Bitmap(_VARIABLE_IMAGE_WIDTH, varHeight);
                gp = Graphics.FromImage(vmap);
                gp.FillRectangle(Brushes.White, new Rectangle(0, 0, vmap.Width, vmap.Height));
                Pen p = new Pen(Brushes.Black, Constants.PEN_WIDTH);
                gp.DrawRectangle(p, new Rectangle(0, 0, vmap.Width, vmap.Height));
                gp.DrawLine(p, new Point(0, (int)sz.Height + 2), new Point(_VARIABLE_IMAGE_WIDTH, (int)sz.Height + 2));
                gp.DrawLine(p,new Point(_VARIABLE_NAME_WIDTH,(int)sz.Height + 2),new Point(_VARIABLE_NAME_WIDTH,vmap.Height));
                gp.DrawString("Variables", Constants.FONT, Brushes.Black, new PointF((vmap.Width - sz.Width) / 2, 2));
                int curY = (int)sz.Height+2;
                for (int x = 0; x < keys.Length; x++)
                {
                    string label = keys[x];
                    SizeF szLabel = gp.MeasureString(keys[x], Constants.FONT);
                    while (szLabel.Width > _VARIABLE_NAME_WIDTH)
                    {
                        if (label.EndsWith("..."))
                            label = label.Substring(0, label.Length - 4) + "...";
                        else
                            label = label.Substring(0, label.Length - 1) + "...";
                        szLabel = gp.MeasureString(label, Constants.FONT);
                    }
                    string val = (_state[null, keys[x]] == null ? "" : _state[null, keys[x]].ToString());
                    SizeF szValue = gp.MeasureString(val, Constants.FONT);
                    if (szValue.Width > _VARIABLE_VALUE_WIDTH)
                    {
                        if (val.EndsWith("..."))
                            val = val.Substring(0, val.Length - 4) + "...";
                        else
                            val = val.Substring(0, val.Length - 1) + "...";
                        szValue = gp.MeasureString(val, Constants.FONT);
                    }
                    gp.DrawString(label, Constants.FONT, Brushes.Black, new Point(2, curY));
                    gp.DrawString(val, Constants.FONT, Brushes.Black, new Point(2 + _VARIABLE_NAME_WIDTH, curY));
                    curY += (int)Math.Max(szLabel.Height, szValue.Height) + 2;
                    gp.DrawLine(p, new Point(0, curY), new Point(_VARIABLE_IMAGE_WIDTH, curY));
                }
                gp.Flush();
                Bitmap tret = new Bitmap(ret.Width + _DEFAULT_PADDING + vmap.Width, Math.Max(ret.Height, vmap.Height + _DEFAULT_PADDING));
                gp = Graphics.FromImage(tret);
                gp.FillRectangle(Brushes.White, new Rectangle(0, 0, tret.Width, tret.Height));
                gp.DrawImage(ret, new Point(0, 0));
                gp.DrawImage(vmap, new Point(ret.Width + _DEFAULT_PADDING, _DEFAULT_PADDING));
                gp.Flush();
                ret = tret;
            }
            return ret;
        }

        public byte[] Animate(bool outputVariables)
        {
            MagickImageCollection collection = new MagickImageCollection();
            _state.Path.StartAnimation();
            while (_state.Path.HasNext())
            {
                collection.Add(new MagickImage(Diagram(outputVariables)));
                collection[collection.Count - 1].AnimationDelay = _ANIMATION_DELAY;
                _state.Path.MoveToNextStep();
            }
            _state.Path.FinishAnimation();
            MemoryStream ms = new MemoryStream();
            QuantizeSettings settings = new QuantizeSettings();
            settings.Colors = 256;
            collection.Quantize(settings);
            collection.Optimize();
            collection.Write(ms,MagickFormat.Gif);
            return ms.ToArray();
        }

        public BusinessProcess Clone(bool includeState,bool includeDelegates)
        {
            BusinessProcess ret = new BusinessProcess();
            ret._doc = _doc;
            ret._components = new List<object>(_components.ToArray());
            if (includeState)
                ret._state = _state;
            if (includeDelegates)
            {
                ret.OnEventStarted = OnEventStarted;
                ret.OnEventCompleted = OnEventCompleted;
                ret.OnEventError = OnEventError;
                ret.OnTaskStarted = OnTaskStarted;
                ret.OnTaskCompleted = OnTaskCompleted;
                ret.OnTaskError = OnTaskError;
                ret.OnProcessStarted = OnProcessStarted;
                ret.OnProcessCompleted = OnProcessCompleted;
                ret.OnProcessError = OnProcessError;
                ret.OnSequenceFlowCompleted = OnSequenceFlowCompleted;
                ret.IsEventStartValid = IsEventStartValid;
                ret.IsProcessStartValid = IsProcessStartValid;
                ret.IsFlowValid = IsFlowValid;
                ret.ProcessBusinessRuleTask = ProcessBusinessRuleTask;
                ret.BeginManualTask = BeginManualTask;
                ret.ProcessRecieveTask = ProcessRecieveTask;
                ret.ProcessScriptTask = ProcessScriptTask;
                ret.ProcessSendTask = ProcessSendTask;
                ret.ProcessServiceTask = ProcessServiceTask;
                ret.ProcessTask = ProcessTask;
                ret.BeginUserTask = BeginUserTask;
            }
            return ret;
        }

        public bool BeginProcess(ProcessVariablesContainer variables)
        {
            if (_isProcessStartValid==null)
                throw new Exception("You must set the delegate IsProcessStartValid in order to start a process.");
            if (_isEventStartValid == null)
                throw new Exception("You must set the delegate IsEventStartValid in order to start a process.");
            bool ret = false;
            foreach (IElement elem in _FullElements)
            {
                if (elem is Process)
                {
                    if (_isProcessStartValid(elem, variables))
                    {
                        Process p = (Process)elem;
                        foreach (StartEvent se in p.StartEvents)
                        {
                            if (_isEventStartValid(se,variables)){
                                if (_onEventStarted!=null)
                                    _onEventStarted(se);
                                _state.Path.StartEvent(se, null);
                                foreach (string str in variables.Keys)
                                    _state[se.id,str]=variables[str];
                                _state.Path.SucceedEvent(se);
                                if (_onEventCompleted!=null)
                                    _onEventCompleted(se);
                                ret=true;
                            }
                        }
                    }
                }
                if (ret)
                    break;
            }
            return ret;
        }

        private void _ProcessStepComplete(string sourceID,string outgoingID) {
            if (outgoingID != null)
            {
                foreach (IElement elem in _FullElements)
                {
                    if (elem.id == outgoingID)
                        _ProcessElement(sourceID,elem);
                }
            }
        }

        private void _ProcessStepError(IElement step) {
            if (_isEventStartValid != null)
            {
                Definition def = null;
                foreach (IElement elem in _Elements)
                {
                    if (elem is Definition)
                    {
                        if (((Definition)elem).LocateElement(step.id) != null)
                        {
                            def = (Definition)elem;
                            break;
                        }
                    }
                }
                if (def != null)
                {
                    foreach (IElement elem in _FullElements)
                    {
                        if (elem is IntermediateCatchEvent)
                        {
                            if (_isEventStartValid(elem, new ProcessVariablesContainer(step.id, _state)))
                            {
                                _ProcessElement(step.id, elem);
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void _ProcessElement(string sourceID,IElement elem)
        {
            if (elem is SequenceFlow)
            {
                SequenceFlow sf = (SequenceFlow)elem;
                lock (_state)
                {
                    _state.Path.ProcessSequenceFlow(sf);
                }
                if (_onSequenceFlowCompleted != null)
                    _onSequenceFlowCompleted(sf);
            }
            else if (elem is AGateway)
            {
                AGateway gw = (AGateway)elem;
                Definition def = null;
                foreach (IElement e in _Elements){
                    if (e is Definition){
                        if (((Definition)e).LocateElement(gw.id)!=null){
                            def = (Definition)e;
                            break;
                        }
                    }
                }
                lock (_state)
                {
                    _state.Path.StartGateway(gw, sourceID);
                }
                if (_onGatewayStarted != null)
                    _onGatewayStarted(gw);
                string[] outgoings = null;
                try
                {
                    outgoings = gw.EvaulateOutgoingPaths(def, _isFlowValid, new ProcessVariablesContainer(elem.id, _state));
                }
                catch (Exception e)
                {
                    if (_onGatewayError != null)
                        _onGatewayError(gw);
                    outgoings = null;
                }
                lock (_state)
                {
                    if (outgoings == null)
                        _state.Path.FailGateway(gw);
                    else
                        _state.Path.SuccessGateway(gw, outgoings);
                }
            }
            else if (elem is AEvent)
            {
                AEvent evnt = (AEvent)elem;
                if (_onEventStarted != null)
                    _onEventStarted(evnt);
                lock (_state)
                {
                    _state.Path.StartEvent(evnt, sourceID);
                }
                bool success = false;
                if (_isEventStartValid != null)
                {
                    try
                    {
                        success = _isEventStartValid(evnt, new ProcessVariablesContainer(evnt.id, _state));
                    }
                    catch (Exception e)
                    {
                        success = false;
                    }
                }
                if (!success){
                    lock (_state) { _state.Path.FailEvent(evnt); }
                    if (_onEventError != null)
                        _onEventError(evnt);
                } else{
                    lock (_state) { _state.Path.SucceedEvent(evnt); }
                    if (_onEventCompleted != null)
                        _onEventCompleted(evnt);
                }
            }
            else if (elem is ATask)
            {
                ATask tsk = (ATask)elem;
                if (_onTaskStarted != null)
                    _onTaskStarted(tsk);
                lock (_state)
                {
                    _state.Path.StartTask(tsk, sourceID);
                }
                try
                {
                    ProcessVariablesContainer variables = new ProcessVariablesContainer(tsk.id,_state);
                    switch (elem.GetType().Name)
                    {
                        case "BusinessRuleTask":
                            _processBusinessRuleTask(tsk, ref variables);
                            _MergeVariables(tsk, variables);
                            break;
                        case "ManualTask":
                            _beginManualTask(tsk, variables, new CompleteManualTask(_CompleteExternalTask), new ErrorManualTask(_ErrorExternalTask));
                            break;
                        case "RecieveTask":
                            _processRecieveTask(tsk, ref variables);
                            _MergeVariables(tsk, variables);
                            break;
                        case "ScriptTask":
                            _processScriptTask(tsk, ref variables);
                            _MergeVariables(tsk, variables);
                            break;
                        case "SendTask":
                            _processSendTask(tsk, ref variables);
                            _MergeVariables(tsk, variables);
                            break;
                        case "ServiceTask":
                            _processServiceTask(tsk, ref variables);
                            _MergeVariables(tsk, variables);
                            break;
                        case "Task":
                            _processTask(tsk, ref variables);
                            _MergeVariables(tsk, variables);
                            break;
                        case "UserTask":
                            Lane ln = null;
                            foreach (IElement e in _FullElements){
                                if (e is Lane){
                                    if (new List<string>(((Lane)e).Nodes).Contains(tsk.id)){
                                        ln = (Lane)e;
                                        break;
                                    }
                                }
                            }
                            _beginUserTask(tsk, variables, ln, new CompleteUserTask(_CompleteExternalTask), new ErrorUserTask(_ErrorExternalTask));
                            break;
                    }
                }
                catch (Exception e)
                {
                    if (_onTaskError != null)
                        _onTaskError(tsk);
                    lock (_state) { _state.Path.FailTask(tsk); }
                }
            }
        }

        private void _MergeVariables(ATask task, ProcessVariablesContainer variables)
        {
            lock (_state)
            {
                foreach (string str in variables.Keys)
                {
                    if (variables[str] == null && _state[task.id, str] != null)
                        _state[task.id, str] = null;
                    else if (_state[task.id, str] == null && variables[str] != null)
                        _state[task.id, str] = variables[str];
                    else if (_state[task.id, str] != null && variables[str] != null)
                    {
                        try
                        {
                            if (!variables[str].Equals(_state[task.id, str]))
                                _state[task.id, str] = variables[str];
                        }
                        catch (Exception e)
                        {
                            _state[task.id, str] = variables[str];
                        }
                    }
                }
                if (_onTaskCompleted != null)
                    _onTaskCompleted(task);
                _state.Path.SucceedTask(task);
            }
        }
    }
}