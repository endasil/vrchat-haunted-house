using UdonSharp;

using UnityEngine;
using UnityEngine.AI;
#pragma warning disable IDE0056

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class AIPathDebugLine : UdonSharpBehaviour
{
    public NavMeshAgent navAi;
    public GhostAISearching ghostAI;
    
    public LineRenderer ArrivalLineRenderer;
    public GameObject markerPrefab;
    public int maxMarkers = 20;

    public Color ArrivalMarkerColor = Color.red;

    private LineRenderer _lineRenderer;
    private GameObject[] _markers = new GameObject[0];
    private Renderer[] _markerRenderers = new Renderer[0];
    private Color _defaultMarkerColor = Color.white;
    private Vector3[] _lastCorners = new Vector3[0];
    private Color _initialStartColor;
    private Color _initialEndColor;

    private void Start()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.startWidth = 0.05f;
        _lineRenderer.endWidth = 0.05f;

        _initialStartColor = _lineRenderer.startColor;
        _initialEndColor = _lineRenderer.endColor;

        if (!navAi)
        {
            navAi = GetComponentInParent<NavMeshAgent>();
        }

        if (navAi == null)
        {
            Debug.LogError("[AIPathDebugLine] No NavMeshAgent found!", this);
        }

        _markers = new GameObject[maxMarkers];
        _markerRenderers = new Renderer[maxMarkers];
        for (int i = 0; i < maxMarkers; i++)
        {
            _markers[i] = VRCInstantiate(markerPrefab);
            _markers[i].SetActive(false);
            _markerRenderers[i] = _markers[i].GetComponentInChildren<Renderer>();
        }

        if (_markerRenderers.Length > 0 && _markerRenderers[0] != null)
            _defaultMarkerColor = _markerRenderers[0].material.color;
    }

    private void Update()
    {
        if (!navAi || !navAi.hasPath || navAi.pathPending || navAi.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            _lineRenderer.positionCount = 0;
            if (ArrivalLineRenderer != null) ArrivalLineRenderer.positionCount = 0;
            ClearMarkers();
            _lastCorners = new Vector3[0];
            return;
        }

        if (navAi.isStopped)
        {
            _lineRenderer.startColor = Color.black;
            _lineRenderer.endColor = Color.black;
        }
        else
        {
            _lineRenderer.startColor = _initialStartColor;
            _lineRenderer.endColor = _initialEndColor;
        }

        Vector3[] corners = navAi.path.corners;

        if (!CornersMatch(_lastCorners, corners))
        {
            _lineRenderer.positionCount = corners.Length;
            _lineRenderer.SetPositions(corners);
            _lastCorners = corners;
            UpdateMarkers(corners);
        }

        for (int i = 0; i < corners.Length - 1; i++)
        {
            Debug.DrawLine(corners[i], corners[i + 1], Color.white);
        }

        if (corners.Length > 0)
        {
            Debug.DrawLine(corners[corners.Length - 1], navAi.destination, Color.yellow);
        }

        if (ArrivalLineRenderer != null)
        {
            if (corners.Length >= 2)
            {
                Vector3 lastCorner = corners[corners.Length - 1];
                Vector3 secondToLast = corners[corners.Length - 2];
                Vector3 arrivalDir = (lastCorner - secondToLast).normalized;
                Vector3 rayOrigin = lastCorner;
                ArrivalLineRenderer.positionCount = 2;
                ArrivalLineRenderer.SetPosition(0, rayOrigin);
                ArrivalLineRenderer.SetPosition(1, rayOrigin + arrivalDir * 1.5f);
            }
            else
            {
                ArrivalLineRenderer.positionCount = 0;
            }
        }
    }

    private void UpdateMarkers(Vector3[] corners)
    {
        ClearMarkers();
        for (int i = 0; i < corners.Length && i < _markers.Length; i++)
        {
            _markers[i].transform.position = corners[i];
            _markers[i].name = $"corner {i} ";
            _markers[i].SetActive(true);

            if (_markerRenderers[i] != null)
            {
                bool isLastCorner = i == corners.Length - 1;
                _markerRenderers[i].material.color = isLastCorner ? ArrivalMarkerColor : _defaultMarkerColor;
            }
        }
    }

    private void ClearMarkers()
    {
        for (int i = 0; i < _markers.Length; i++)
        {
            if (_markers[i] != null)
                _markers[i].SetActive(false);
        }
    }

    private bool CornersMatch(Vector3[] a, Vector3[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if ((a[i] - b[i]).sqrMagnitude > 0.001f * 0.001f) return false;
        }
        return true;
    }
}