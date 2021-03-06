﻿#region License

// MIT License
//
// Copyright (c) 2018 Marcus Technical Services, Inc. http://www.marcusts.com
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
// associated documentation files (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute,
// sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
// NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
// OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

#endregion License

namespace SharedForms.Common.Navigation
{
   #region Imports

   using Interfaces;
   using System;
   using Utils;
   using ViewModels;
   using Xamarin.Forms;

   #endregion Imports

   /// <summary>
   /// A controller to manage which views and view models are shown for a given state
   /// </summary>
   public abstract class StateMachineBase : IStateMachineBase
   {
      #region Private Methods

      private static IViewModelBase SeekPageEventProvider(Func<IViewModelBase> viewModelCreator, Page page)
      {
         var viewModel = viewModelCreator?.Invoke();

         if (viewModel != null)
         {
            // Corner case: hard to pass along the page as page event provider when the page is
            // created in an expression, so assigning it here.
            if (viewModel is IReceivePageEvents viewModelAsPageEventsReceiver &&
                page is IProvidePageEvents pageAsPageEventsProvider)
            {
               viewModelAsPageEventsReceiver.PageEventProvider = pageAsPageEventsProvider;
            }
         }

         return viewModel;
      }

      #endregion Private Methods

      #region Private Destructors

      ~StateMachineBase()
      {
         ReleaseUnmanagedResources();
      }

      #endregion Private Destructors

      #region Public Classes

      public class AppStartUpMessage : NoPayloadMessage
      {
      }

      #endregion Public Classes

      #region Private Variables

      private string _lastAppState;

      private Page _lastPage;

      #endregion Private Variables

      #region Public Properties

      public abstract string AppStartUpState { get; }

      public abstract IMenuNavigationState[] MenuItems { get; }

      #endregion Public Properties

      #region Public Methods

      public void Dispose()
      {
         ReleaseUnmanagedResources();
         GC.SuppressFinalize(this);
      }

      public void GoToAppState<T>(string newState, T payload = default(T), bool preventStackPush = false)
      {
         if (_lastAppState.IsSameAs(newState))
         {
            return;
         }

         // Raise an event to notify the nav bar that the back-stack requires modification. Send in
         // the last app state, *not* the new one.
         FormsMessengerUtils.Send(new AppStateChangedMessage(_lastAppState, preventStackPush));

         // CurrentAppState = newState;
         _lastAppState = newState;

         // Not awaiting here because we do not directly change the Application.Current.MainPage.
         // That is done through a message.
         RespondToAppStateChange(newState, payload, preventStackPush);
      }

      public abstract void GoToLandingPage(bool preventStackPush = true);

      // public string CurrentAppState { get; private set; } Sets the startup state for the app on
      // initial start (or restart).
      public void GoToStartUpState()
      {
         FormsMessengerUtils.Send(new AppStartUpMessage());

         GoToAppState<NoPayload>(AppStartUpState, null, true);
      }

      #endregion Public Methods

      #region Protected Methods

      protected void CheckAgainstLastPage(Type pageType, Func<Page> pageCreator, Func<IViewModelBase> viewModelCreator,
         bool preventStackPush)
      {
         // If the same page, keep it
         if (_lastPage != null && _lastPage.GetType() == pageType)
         {
            var viewModel = SeekPageEventProvider(viewModelCreator, _lastPage);

            FormsMessengerUtils.Send
            (
               new MainPageBindingContextChangeRequestMessage
               {
                  Payload = viewModelCreator?.Invoke(),
                  PreventNavStackPush = preventStackPush
               }
            );
            return;
         }

         // ELSE create both the page and view model
         var page = pageCreator?.Invoke();

         if (page != null)
         {
            var viewModel = SeekPageEventProvider(viewModelCreator, page);

            // Unconditional; null is a legal setting
            page.BindingContext = viewModel;

            FormsMessengerUtils.Send(new MainPageChangeRequestMessage
            {
               Payload = page,
               PreventNavStackPush = preventStackPush
            });

            _lastPage = page;
         }
      }

      protected virtual void ReleaseUnmanagedResources()
      {
      }

      protected abstract void RespondToAppStateChange<PayloadT>(string newState, PayloadT payload,
         bool preventStackPush);

      #endregion Protected Methods
   }

   public interface IStateMachineBase : IDisposable
   {
      // A way of knowing the current app state, though this should not be commonly referenced.
      // string CurrentAppState { get; }

      #region Public Properties

      IMenuNavigationState[] MenuItems { get; }

      #endregion Public Properties

      #region Public Methods

      // The normal way of changing states
      void GoToAppState<T>(string newState, T payload = default(T), bool preventStackPush = false);

      // Goes to the default landing page; for convenience only
      void GoToLandingPage(bool preventStackPush = true);

      // Sets the startup state for the app on initial start (or restart).
      void GoToStartUpState();

      #endregion Public Methods
   }
}
