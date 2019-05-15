﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Model;
using Xpand.XAF.Modules.Reactive.Extensions;
using Xpand.XAF.Modules.Reactive.Services;

namespace Xpand.XAF.Modules.CloneMemberValue{
    public static class CloneMemberValueService{

        public static IObservable<Unit> Connect(this ApplicationModulesManager modulesManager ,XafApplication application){
            if (application != null){
                return application.WhenCloneMemberValues()
                    .Tracer(true)
                    .ToUnit();
            }
            return Observable.Empty<Unit>();
        }

        public static IEnumerable<IModelCommonMemberViewItemCloneValue> CloneValueMemberViewItems(this IModelObjectView modelObjectView) {
            return (modelObjectView is IModelListView view? view.Columns.Cast<IModelCommonMemberViewItemCloneValue>()
                    : ((IModelDetailView) modelObjectView).Items.OfType<IModelCommonMemberViewItemCloneValue>())
                .Where(state => state.CloneValue);
        }

        public static IObservable<(IModelObjectView modelObjectView, IMemberInfo MemberInfo, IObjectSpaceLink previousObject, IObjectSpaceLink currentObject)> CloneMembers(
            this IObservable<(IModelObjectView modelObjectView,IObjectSpaceLink previous, IObjectSpaceLink current)> source){
            
            return  source.SelectMany(_ => _.modelObjectView
                .CloneValueMemberViewItems()
                .Select(value => (_.modelObjectView, ((IModelMemberViewItem) value).ModelMember.MemberInfo,_.previous,_.current).CloneMemberValue()))
                ;
        }

        public static IObservable<(IObjectSpaceLink previous, IObjectSpaceLink current)> NewObjectPairs(this ListEditor listEditor){
            return listEditor.WhenNewObjectAdding()
                .Select(_ => _.e.AddedObject).Cast<IObjectSpaceLink>()
                .CombineWithPrevious().Where(_ => _.previous != null);
        }

        public static IObservable<(DetailView previous, DetailView current)> WhenCloneMemberValueDetailViewPairs(this XafApplication application){
            return application
                .WhenDetailViewCreated()
                .Select(_ => _.e.View).Where(view => view.Model.CloneValueMemberViewItems().Any())
                .CombineWithPrevious().Where(_ => _.previous != null&&_.current.ObjectSpace.IsNewObject(_.current.CurrentObject))
                .Publish().RefCount();
        }

        public static IObservable<ListView> WhenCloneMemberValueListViewCreated(this XafApplication application){
            return application
                .WhenListViewCreated().Where(_ => _.e.ListView.Model.CloneValueMemberViewItems().Any())
                .Select(_ => _.e.ListView).Where(view => view.Model.AllowEdit);
        }

        public static IObservable<(IModelObjectView modelObjectView, IMemberInfo MemberInfo, IObjectSpaceLink previousObject, IObjectSpaceLink currentObject)> WhenCloneMemberValues(this XafApplication application){
            return application.WhenCloneMemberValueDetailViewPairs()
                .Select(_ => (_.current.Model.AsObjectView,(IObjectSpaceLink)_.previous.CurrentObject,(IObjectSpaceLink)_.current.CurrentObject))
                .Merge(application.WhenCloneMemberValueListViewCreated()
                    .ControlsCreated()
                    .SelectMany(_ => (_.view.Editor
                        .NewObjectPairs()
                        .Select(tuple => (_.view.Model.AsObjectView,tuple.previous,tuple.current)))))
                .CloneMembers()
                .Publish().RefCount();
        }

        private static (IModelObjectView modelObjectView,IMemberInfo MemberInfo, IObjectSpaceLink previousObject, IObjectSpaceLink currentObject)
            CloneMemberValue(this (IModelObjectView modelObjectView,IMemberInfo MemberInfo, IObjectSpaceLink previousObject, IObjectSpaceLink currentObject) _){

            var value = _.MemberInfo.GetValue(_.previousObject);
            if (_.MemberInfo.MemberTypeInfo.IsPersistent){
                value = _.currentObject.ObjectSpace.GetObject(value);
            }
            _.MemberInfo.SetValue(_.currentObject, value);
            return (_.modelObjectView,_.MemberInfo, _.previousObject, _.currentObject);
        }
    }
}