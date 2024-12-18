using System.Collections;
using UnityEditor;

namespace Kingmaker.Editor.Utility
{
	public class EditorCoroutine
	{
		public static EditorCoroutine Start(IEnumerator routine)
		{
			EditorCoroutine coroutine = new EditorCoroutine(routine);
			coroutine.Start();
			return coroutine;
		}

		private readonly IEnumerator m_Routine;

		private EditorCoroutine(IEnumerator routine)
		{
			m_Routine = routine;
		}

		private void Start()
		{
			//Debug.Log("start");
			EditorApplication.update += Update;
		}

		public void Stop()
		{
			//Debug.Log("stop");
			EditorApplication.update -= Update;
		}

		private void Update()
		{
			/* NOTE: no need to try/catch MoveNext,
			 * if an IEnumerator throws its next iteration returns false.
			 * Also, Unity probably catches when calling EditorApplication.update.
			 */

			//Debug.Log("update");
			if (!m_Routine.MoveNext())
			{
				Stop();
			}
		}
	}
}