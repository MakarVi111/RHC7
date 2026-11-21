using System;
using UnityEditor;
using UnityEngine;

namespace RHC7.EditorTools
{
    /// <summary>
    /// On entering play mode, focus the Game view so the very first LMB drag
    /// reaches the game instead of being eaten by the editor focus change.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeGameFocus
    {
        static PlayModeGameFocus()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.delayCall += FocusGameView;
        }

        private static void FocusGameView()
        {
            Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null) return;

            var windows = UnityEngine.Resources.FindObjectsOfTypeAll(gameViewType);
            foreach (var window in windows)
            {
                var editorWindow = window as EditorWindow;
                if (editorWindow == null) continue;
                editorWindow.Focus();
                return;
            }
        }
    }
}
