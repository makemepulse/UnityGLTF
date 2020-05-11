
using UnityEngine;

public abstract class SerializedSettings<T> : ScriptableObject where T : new () {
  
  [SerializeField]
  private T m_info = new T ();

  public T info {
    get { return m_info; }
    set { m_info = value; }
  }
}