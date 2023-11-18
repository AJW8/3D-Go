using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class Game : MonoBehaviour
{
    public GameObject background;
    public Color backgroundColour;
    public Color boardColour;
    public Color player1Colour;
    public Color player2Colour;
    public Color invalidColour;
    public int complexity; // [3, 7]
    public float tileThickness;
    public float tileBevelFactor;
    public float komi;
    public GameObject tilePrefab;
    public GameObject stonePrefab;
    private GameObject[] boardTiles;
    private GameObject[] slotTiles;
    private Vector3[] slotPositions;
    private GameObject[] boardStones;
    private GameObject currentStone;
    private float stoneScale;
    private int[] connections;
    private bool[] player1Occupied;
    private bool[] player2Occupied;
    private int[] player1Groups;
    private int[] player2Groups;
    private bool[] validMove;
    private int selectedSlot;
    private bool mouseDown;
    private Quaternion boardOrigin;
    private Vector3 mouseOrigin;
    private bool player1Turn;
    private int player1Prisoners;
    private int player2Prisoners;
    private bool gameOver;

    void Awake()
    {
        CreateBoard();
        if (gameOver) return;
        int c1 = 10;
        int c2 = 20;
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        for (int i = 0; i < c1; i++)
        {
            float z = -Mathf.Cos(i * Mathf.PI / c1);
            if (i == 0)
            {
                vertices.Add(new Vector3(0, 0, z));
                for (int j = 0; j < c2; j++)
                {
                    triangles.Add(0);
                    triangles.Add(j + 1);
                    triangles.Add((j + 1) % c2 + 1);
                }
            }
            for (int j = 0; j < c2; j++)
            {
                float m = Mathf.Sqrt(1 - z * z);
                float a = (j + (i % 2) / 2f) * 2 * Mathf.PI / c2;
                vertices.Add(new Vector3(m * Mathf.Sin(a), m * Mathf.Cos(a), z));
                if (i > 0)
                {
                    triangles.Add((i - 1) * c2 + j + 1);
                    triangles.Add(i * c2 + (j + 1 - i % 2) % c2 + 1);
                    triangles.Add((i - 1) * c2 + (j + 1) % c2 + 1);
                    triangles.Add((i - 1) * c2 + (j + 1) % c2 + 1);
                    triangles.Add(i * c2 + (j + 1 - i % 2) % c2 + 1);
                    triangles.Add(i * c2 + (j + 2 - i % 2) % c2 + 1);
                }
            }
            if (i == c1 - 1)
            {
                vertices.Add(new Vector3(0, 0, z));
                for (int j = 0; j < c2; j++)
                {
                    triangles.Add(i * c2 + (j + 1) % c2 + 1);
                    triangles.Add(i * c2 + j + 1);
                    triangles.Add(vertices.Count - 1);
                }
            }
        }
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 v = new Vector3(vertices[i].x, vertices[i].y, vertices[i].z / (vertices[i].z < 0 ? 2f : 4));
            vertices.RemoveAt(i);
            vertices.Insert(i, v);
        }
        Mesh mesh = new Mesh()
        {
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray()
        };
        mesh.RecalculateNormals();
        //AssetDatabase.CreateAsset(mesh, "Assets/Stone1.Asset");
    }

    // Start is called before the first frame update
    void Start()
    {
        background.GetComponent<MeshRenderer>().material.color = backgroundColour; // f1c38e ecb069 fdeac1 d2b04c e0a030 a06204 c8651b c87620 f8D452 d19a30 f3cc3C fac218 f7cb2d
        stoneScale = 0.6f / (complexity + 1);
        selectedSlot = -1;
        boardOrigin = transform.rotation;
        NewGame();
    }

    private void CreateBoard()
    {
        Vector3[] vertices = new Vector3[8];
        for (int i = 0; i < 8; i++) vertices[i] = new Vector3(2 * (i / 4) - 1, 2 * ((i % 4) / 2) - 1, 2 * (i % 2) - 1).normalized;
        int[] edges = new int[] { 0, 1, 0, 2, 0, 4, 1, 3, 1, 5, 2, 3, 2, 6, 3, 7, 4, 5, 4, 6, 5, 7, 6, 7 };
        int[] squares = new int[] { 0, 1, 3, 2, 0, 4, 5, 1, 0, 2, 6, 4, 1, 5, 7, 3, 2, 3, 7, 6, 4, 6, 7, 5 };
        List<Vector3> newVertices = new List<Vector3>(vertices);
        List<int> newTriangles = new List<int>();
        List<int> newSquares = new List<int>();
        for (int i = 0; i < edges.Length; i += 2)
        {
            Vector3 v1 = vertices[edges[i]];
            Vector3 v2 = vertices[edges[i + 1]];
            //for (int j = 0; j < complexity; j++) newVertices.Add((v1 + (v2 - v1) * (j + 1f) / (complexity + 1)).normalized);
            transform.rotation = Quaternion.LookRotation(v1, v2);
            float angle = Vector3.Angle(v1, v2);
            for (int j = 0; j < complexity; j++)
            {
                transform.Rotate(-angle / (complexity + 1), 0, 0, Space.Self);
                newVertices.Add(transform.forward);
            }
            transform.rotation = boardOrigin;
        }
        for (int i = 0; i < squares.Length; i += 4)
        {
            int v1 = squares[i];
            int v2 = squares[i + 1];
            int v3 = squares[i + 2];
            int v4 = squares[i + 3];
            int e1 = -1;
            int e2 = -1;
            int e3 = -1;
            int e4 = -1;
            for (int j = 0; j < edges.Length; j += 2)
            {
                if ((v1 == edges[j] || v1 == edges[j + 1]) && (v2 == edges[j] || v2 == edges[j + 1])) e1 = vertices.Length + j * complexity / 2;
                else if ((v1 == edges[j] || v1 == edges[j + 1]) && (v4 == edges[j] || v4 == edges[j + 1])) e2 = vertices.Length + j * complexity / 2;
                else if ((v2 == edges[j] || v2 == edges[j + 1]) && (v3 == edges[j] || v3 == edges[j + 1])) e3 = vertices.Length + j * complexity / 2;
                else if ((v3 == edges[j] || v3 == edges[j + 1]) && (v4 == edges[j] || v4 == edges[j + 1])) e4 = vertices.Length + j * complexity / 2;
            }
            int[] currentRow = new int[complexity + 2];
            currentRow[0] = v1;
            for (int j = 0; j < complexity; j++) currentRow[j + 1] = e1 + j;
            currentRow[complexity + 1] = v2;
            for (int j = 0; j < complexity + 1; j++)
            {
                int[] newRow = new int[complexity + 2];
                if (j < complexity)
                {
                    newRow[0] = e2 + j;
                    for (int k = 0; k < complexity; k++) newRow[k + 1] = newVertices.Count + k;
                    newRow[complexity + 1] = e3 + (v2 < v3 ? j : complexity - 1 - j);
                }
                else
                {
                    newRow[0] = v4;
                    for (int k = 0; k < complexity; k++) newRow[k + 1] = e4 + (v3 > v4 ? k : complexity - 1 - k);
                    newRow[complexity + 1] = v3;
                }
                for (int k = 0; k < complexity + 1; k++)
                {
                    newSquares.Add(currentRow[k]);
                    newSquares.Add(currentRow[k + 1]);
                    newSquares.Add(newRow[k + 1]);
                    newSquares.Add(newRow[k]);
                }
                if (j < complexity)
                {
                    Vector3 newRowStart = newVertices[newRow[0]];
                    Vector3 newRowEnd = newVertices[newRow[complexity + 1]];
                    //for (int k = 0; k < complexity; k++) newVertices.Add((newRowStart + (newRowEnd - newRowStart) * (k + 1f) / (complexity + 1)).normalized);
                    transform.rotation = Quaternion.LookRotation(newRowStart, newRowEnd);
                    float angle = Vector3.Angle(newRowStart, newRowEnd);
                    for (int k = 0; k < complexity; k++)
                    {
                        transform.Rotate(-angle / (complexity + 1), 0, 0, Space.Self);
                        newVertices.Add(transform.forward);
                    }
                    transform.rotation = boardOrigin;
                }
                currentRow = newRow;
            }
        }
        List<Vector3> dualVertices = new List<Vector3>();
        List<int> dualTriangles = new List<int>();
        List<int> dualSquares = new List<int>();
        for (int i = 0; i < newVertices.Count; i++)
        {
            List<int> adjacentFaces = new List<int>();
            List<int> previousPoints = new List<int>();
            List<int> nextPoints = new List<int>();
            for (int j = 0; j < newTriangles.Count; j++)
            {
                if (i == newTriangles[j])
                {
                    adjacentFaces.Add(j / 3);
                    previousPoints.Add(newTriangles[j % 3 == 0 ? j + 2 : j - 1]);
                    nextPoints.Add(newTriangles[j % 3 < 2 ? j + 1 : j - 2]);
                }
            }
            for (int j = 0; j < newSquares.Count; j++)
            {
                if (i == newSquares[j])
                {
                    adjacentFaces.Add(newTriangles.Count / 3 + j / 4);
                    previousPoints.Add(newSquares[j % 4 == 0 ? j + 3 : j - 1]);
                    nextPoints.Add(newSquares[j % 4 < 3 ? j + 1 : j - 3]);
                }
            }
            if (adjacentFaces.Count == 3)
            {
                if (previousPoints[0] == nextPoints[1] && previousPoints[1] == nextPoints[2] && previousPoints[2] == nextPoints[0])
                {
                    dualTriangles.Add(adjacentFaces[0]);
                    dualTriangles.Add(adjacentFaces[1]);
                    dualTriangles.Add(adjacentFaces[2]);
                }
                else if (previousPoints[0] == nextPoints[2] && previousPoints[1] == nextPoints[0] && previousPoints[2] == nextPoints[1])
                {
                    dualTriangles.Add(adjacentFaces[0]);
                    dualTriangles.Add(adjacentFaces[2]);
                    dualTriangles.Add(adjacentFaces[1]);
                }
            }
            else if (adjacentFaces.Count == 4)
            {
                int currentIndex = 0;
                do
                {
                    dualSquares.Add(adjacentFaces[currentIndex]);
                    bool foundNext = false;
                    for (int j = 0; j < adjacentFaces.Count; j++)
                    {
                        if (!foundNext && j != currentIndex && previousPoints[currentIndex] == nextPoints[j])
                        {
                            currentIndex = j;
                            foundNext = true;
                        }
                    }
                }
                while (currentIndex != 0);
            }
        }
        Vector3 newPoint = new Vector3();
        for (int i = 0; i < newTriangles.Count; i++)
        {
            newPoint += newVertices[newTriangles[i]] / 3;
            if (i % 3 == 2)
            {
                dualVertices.Add(newPoint.normalized);
                newPoint = new Vector3();
            }
        }
        for (int i = 0; i < newSquares.Count; i++)
        {
            newPoint += newVertices[newSquares[i]] / 4;
            if (i % 4 == 3)
            {
                dualVertices.Add(newPoint.normalized);
                newPoint = new Vector3();
            }
        }
        newVertices = dualVertices;
        newTriangles = dualTriangles;
        newSquares = dualSquares;
        slotPositions = newVertices.ToArray();
        List<int> connectionList = new List<int>();
        for (int i = 0; i < newVertices.Count; i++)
        {
            for (int j = 0; j < newSquares.Count; j++)
            {
                if (i == newSquares[j])
                {
                    int next = newSquares[j % 4 < 3 ? j + 1 : j - 3];
                    if (i < next)
                    {
                        connectionList.Add(i);
                        connectionList.Add(next);
                    }
                }
            }
        }
        connections = connectionList.ToArray();
        if (boardTiles != null)
        {
            foreach (GameObject tile in boardTiles) if (tile != null) Destroy(tile);
            boardTiles = null;
        }
        boardTiles = new GameObject[newTriangles.Count / 3 + newSquares.Count / 4];
        Vector3[] tileCentres = new Vector3[boardTiles.Length];
        for (int i = 0; i < newTriangles.Count; i++)
        {
            if (i % 3 == 0) tileCentres[i / 3] = newVertices[newTriangles[i]] / 3;
            else tileCentres[i / 3] += newVertices[newTriangles[i]] / 3;
        }
        for (int i = 0; i < newSquares.Count; i++)
        {
            if (i % 4 == 0) tileCentres[newTriangles.Count / 3 + i / 4] = newVertices[newSquares[i]] / 4;
            else tileCentres[newTriangles.Count / 3 + i / 4] += newVertices[newSquares[i]] / 4;
        }
        List<Vector3> tileVertices = new List<Vector3>();
        for (int i = 0; i < newTriangles.Count; i++)
        {
            tileVertices.Add(newVertices[newTriangles[i]] / (1 + tileThickness));
            tileVertices.Add(newVertices[newTriangles[i]] + (newVertices[newTriangles[i % 3 < 2 ? i + 1 : i - 2]] - newVertices[newTriangles[i]]) * tileBevelFactor / 2);
            tileVertices.Add(newVertices[newTriangles[i]] + (newVertices[newTriangles[i % 3 == 0 ? i + 2 : i - 1]] - newVertices[newTriangles[i]]) * tileBevelFactor / 2);
            tileVertices.Add((newVertices[newTriangles[i]] + (tileCentres[i / 3].normalized - newVertices[newTriangles[i]]) * tileBevelFactor / 2).normalized);
            if (i % 3 == 2)
            {
                boardTiles[i / 3] = Instantiate(tilePrefab, transform.position, transform.rotation);
                boardTiles[i / 3].transform.SetParent(transform);
                MeshFilter tileFilter = boardTiles[i / 3].GetComponent<MeshFilter>();
                tileFilter.mesh = new Mesh()
                {
                    vertices = tileVertices.ToArray(),
                    triangles = new int[] { 0, 1, 2, 1, 3, 2, 4, 5, 6, 5, 7, 6, 8, 9, 10, 9, 11, 10, 1, 6, 3, 1, 6, 7, 5, 10, 7, 5, 10, 11, 2, 3, 9, 3, 11, 9, 3, 7, 11 }
                };
                tileFilter.mesh.RecalculateNormals();
                boardTiles[i / 3].GetComponent<MeshCollider>().sharedMesh = tileFilter.mesh;
                boardTiles[i / 3].GetComponent<Renderer>().material.color = boardColour; // d2b04c
                tileVertices = new List<Vector3>();
            }
        }
        for (int i = 0; i < newSquares.Count; i++)
        {
            int tileIndex = newTriangles.Count / 3 + i / 4;
            tileVertices.Add(newVertices[newSquares[i]] / (1 + tileThickness));
            tileVertices.Add(newVertices[newSquares[i]] + (newVertices[newSquares[i % 4 < 3 ? i + 1 : i - 3]] - newVertices[newSquares[i]]) * tileBevelFactor / 2);
            tileVertices.Add(newVertices[newSquares[i]] + (newVertices[newSquares[i % 4 == 0 ? i + 3 : i - 1]] - newVertices[newSquares[i]]) * tileBevelFactor / 2);
            tileVertices.Add((newVertices[newSquares[i]] + (tileCentres[tileIndex].normalized - newVertices[newSquares[i]]) * tileBevelFactor / 2).normalized);
            if (i % 4 == 3)
            {
                boardTiles[tileIndex] = Instantiate(tilePrefab, transform.position, transform.rotation);
                boardTiles[tileIndex].transform.SetParent(transform);
                MeshFilter tileFilter = boardTiles[tileIndex].GetComponent<MeshFilter>();
                tileFilter.mesh = new Mesh()
                {
                    vertices = tileVertices.ToArray(),
                    triangles = new int[] { 0, 1, 2, 1, 3, 2, 4, 5, 6, 5, 7, 6, 8, 9, 10, 9, 11, 10, 12, 13, 14, 13, 15, 14, 1, 7, 3, 1, 6, 7, 5, 11, 7, 5, 10, 11, 9, 15, 11, 9, 14, 15, 3, 15, 13, 2, 3, 13, 3, 7, 11, 3, 11, 15 }
                };
                tileFilter.mesh.RecalculateNormals();
                boardTiles[tileIndex].GetComponent<MeshCollider>().sharedMesh = tileFilter.mesh;
                boardTiles[tileIndex].GetComponent<Renderer>().material.color = boardColour;
                tileVertices = new List<Vector3>();
            }
        }
    }

    public void NewGame()
    {
        boardStones = new GameObject[slotPositions.Length];
        currentStone = Instantiate(stonePrefab, transform.position, transform.rotation);
        currentStone.transform.localScale = new Vector3(stoneScale, stoneScale, stoneScale);
        player1Occupied = new bool[slotPositions.Length];
        player2Occupied = new bool[slotPositions.Length];
        player1Groups = new int[slotPositions.Length];
        player2Groups = new int[slotPositions.Length];
        selectedSlot = -1;
        mouseDown = false;
        player1Turn = true;
        player1Prisoners = 0;
        player2Prisoners = 0;
        validMove = new bool[slotPositions.Length];
        SetValidMoves();
        gameOver = false;
    }

    public void ClearBoard()
    {
        transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position, Vector3.up);
        if (boardStones != null)
        {
            foreach (GameObject stone in boardStones) if (stone != null) Destroy(stone);
            boardStones = null;
        }
        if (currentStone != null)
        {
            Destroy(currentStone);
            currentStone = null;
        }
    }

    // Update is called once per frame
    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            if (!gameOver)
            {
                Vector3 mouseCurrent = transform.InverseTransformPoint(hit.point);
                bool movedMouse = selectedSlot < 0 || mouseOrigin.x != mouseCurrent.x || mouseOrigin.y != mouseCurrent.y || mouseOrigin.z != mouseCurrent.z;
                bool makingMove = false;
                if (!mouseDown && (selectedSlot >= 0 || hit.transform.gameObject == gameObject) && Input.GetMouseButtonDown(0))
                {
                    mouseDown = true;
                    mouseOrigin = mouseCurrent;
                }
                else if (mouseDown && Input.GetMouseButtonUp(0))
                {
                    mouseDown = false;
                    makingMove = selectedSlot >= 0 && validMove[selectedSlot] && !movedMouse;
                }
                if (!mouseDown && Input.GetMouseButtonDown(0))
                {
                    mouseDown = true;
                    mouseOrigin = mouseCurrent;
                }
                else if (mouseDown && Input.GetMouseButtonUp(0)) mouseDown = false;
                if (mouseDown && !Input.GetMouseButtonDown(0)) transform.localRotation *= Quaternion.FromToRotation(mouseOrigin - transform.position, mouseCurrent - transform.position);
                if (mouseDown && movedMouse && !makingMove) currentStone.SetActive(false);
                else if ((mouseDown || movedMouse) && !makingMove)
                {
                    int newSelectedSlot = -1;
                    float minDistance = 0;
                    for (int i = 0; i < slotPositions.Length; i++)
                    {
                        Vector3 distance = transform.TransformPoint(slotPositions[i]) - hit.point;
                        float modulus = distance.x * distance.x + distance.y * distance.y + distance.z * distance.z;
                        if (minDistance == 0 || modulus < minDistance)
                        {
                            newSelectedSlot = i;
                            minDistance = modulus;
                        }
                    }
                    if (selectedSlot != newSelectedSlot)
                    {
                        if (newSelectedSlot >= 0)
                        {
                            currentStone.SetActive(true);
                            currentStone.transform.position = transform.TransformPoint(slotPositions[newSelectedSlot]) * (1 + stoneScale / 5);
                            currentStone.transform.rotation = Quaternion.LookRotation(-currentStone.transform.position, Camera.main.transform.up);
                            currentStone.GetComponent<Renderer>().material.color = validMove[newSelectedSlot] ? player1Turn ? player1Colour : player2Colour : invalidColour;
                            currentStone.SetActive(player1Groups[newSelectedSlot] == 0 && player2Groups[newSelectedSlot] == 0);
                        }
                        else currentStone.SetActive(false);
                        selectedSlot = newSelectedSlot;
                    }
                }
                if (selectedSlot >= 0 && makingMove) Move();
            }
        }
        else if (selectedSlot >= 0)
        {
            currentStone.SetActive(false);
            selectedSlot = -1;
        }
    }

    private void SetValidMoves()
    {
        for (int i = 0; i < slotPositions.Length; i++) validMove[i] = false;
        int[] friendlyGroups = player1Turn ? player1Groups : player2Groups;
        int[] enemyGroups = player1Turn ? player2Groups : player1Groups;
        bool[] ko = new bool[slotPositions.Length];
        for (int i = 0; i < slotPositions.Length; i++)
        {
            if (!validMove[i] && friendlyGroups[i] == 0 && enemyGroups[i] == 0)
            {
                List<int> adjacentFriendlyGroups = new List<int>();
                List<int> adjacentEnemyGroups = new List<int>();
                for (int j = 0; j < connections.Length; j++)
                {
                    if (i == connections[j])
                    {
                        int next = connections[j % 2 == 0 ? j + 1 : j - 1];
                        if (friendlyGroups[next] > 0 && !adjacentFriendlyGroups.Contains(friendlyGroups[next])) adjacentFriendlyGroups.Add(friendlyGroups[next]);
                        else if (enemyGroups[next] > 0 && !adjacentEnemyGroups.Contains(enemyGroups[next])) adjacentEnemyGroups.Add(enemyGroups[next]);
                        else validMove[i] = true;
                    }
                }
                List<int> doomedEnemyGroups = new List<int>();
                for (int j = 0; j < adjacentEnemyGroups.Count; j++)
                {
                    bool doomed = true;
                    for (int k = 0; k < connections.Length; k++)
                    {
                        if (doomed && adjacentEnemyGroups[j] == enemyGroups[connections[k]])
                        {
                            int next = connections[k % 2 == 0 ? k + 1 : k - 1];
                            if (friendlyGroups[next] == 0 && enemyGroups[next] == 0 && i != next) doomed = false;
                        }
                    }
                    if (doomed) doomedEnemyGroups.Add(adjacentEnemyGroups[j]);
                    if (doomed) validMove[i] = true;
                }
                for (int j = 0; j < adjacentFriendlyGroups.Count; j++)
                {
                    List<int> adjacentFriendlyLiberties = new List<int>();
                    for (int k = 0; k < connections.Length; k++)
                    {
                        if (adjacentFriendlyGroups.Contains(friendlyGroups[connections[k]]))
                        {
                            int next = connections[k % 2 == 0 ? k + 1 : k - 1];
                            if (friendlyGroups[next] == 0 && enemyGroups[next] == 0 && i != next)
                            {
                                validMove[i] = true;
                                adjacentFriendlyLiberties.Add(next);
                            }
                        }
                    }
                    for (int k = 0; k < adjacentFriendlyLiberties.Count; k++) validMove[adjacentFriendlyLiberties[k]] = true;
                }
                bool[] friendlyOccupied = player1Turn ? player1Occupied : player2Occupied;
                if (friendlyOccupied[i] && doomedEnemyGroups.Count > 0)
                {
                    bool[] enemyOccupied = player1Turn ? player2Occupied : player1Occupied;
                    ko[i] = true;
                    for (int j = 0; j < slotPositions.Length; j++) if (ko[i] && (friendlyOccupied[j] != (friendlyGroups[j] > 0)) || (enemyOccupied[j] != (enemyGroups[j] > 0 && !doomedEnemyGroups.Contains(enemyGroups[j])))) ko[i] = false;
                }
            }
        }
        for (int i = 0; i < slotPositions.Length; i++) validMove[i] &= !ko[i];
    }

    private void Move()
    {
        for (int i = 0; i < slotPositions.Length; i++)
        {
            player1Occupied[i] = player1Groups[i] > 0;
            player2Occupied[i] = player2Groups[i] > 0;
        }
        int[] friendlyGroups = player1Turn ? player1Groups : player2Groups;
        int[] enemyGroups = player1Turn ? player2Groups : player1Groups;
        List<int> adjacentFriendlyGroups = new List<int>();
        for (int i = 0; i < connections.Length; i++)
        {
            if (connections[i] == selectedSlot)
            {
                int next = connections[i % 2 == 0 ? i + 1 : i - 1];
                if (friendlyGroups[next] > 0 && !adjacentFriendlyGroups.Contains(friendlyGroups[next])) adjacentFriendlyGroups.Add(friendlyGroups[next]);
            }
        }
        if (adjacentFriendlyGroups.Count == 0)
        {
            bool hasIndex;
            int nextIndex = 1;
            do
            {
                hasIndex = false;
                for (int i = 0; i < slotPositions.Length; i++) if (friendlyGroups[i] == nextIndex) hasIndex = true;
                if (hasIndex) nextIndex++;
            }
            while (hasIndex);
            friendlyGroups[selectedSlot] = nextIndex;
        }
        else
        {
            int minIndex = adjacentFriendlyGroups[0];
            for (int i = 1; i < adjacentFriendlyGroups.Count; i++) if (adjacentFriendlyGroups[i] < minIndex) minIndex = adjacentFriendlyGroups[i];
            friendlyGroups[selectedSlot] = minIndex;
            for (int i = 0; i < slotPositions.Length; i++) if (adjacentFriendlyGroups.Contains(friendlyGroups[i])) friendlyGroups[i] = minIndex;
        }
        List<int> adjacentEnemyGroups = new List<int>();
        for (int i = 0; i < connections.Length; i++)
        {
            if (connections[i] == selectedSlot)
            {
                int next = connections[i % 2 == 0 ? i + 1 : i - 1];
                if (enemyGroups[next] > 0 && !adjacentEnemyGroups.Contains(enemyGroups[next])) adjacentEnemyGroups.Add(enemyGroups[next]);
            }
        }
        int prisoners = player1Turn ? player1Prisoners : player2Prisoners;
        for (int i = 0; i < adjacentEnemyGroups.Count; i++)
        {
            bool doomed = true;
            for (int j = 0; j < connections.Length; j++)
            {
                if (doomed && adjacentEnemyGroups[i] == enemyGroups[connections[j]])
                {
                    int next = connections[j % 2 == 0 ? j + 1 : j - 1];
                    if (friendlyGroups[next] == 0 && enemyGroups[next] == 0) doomed = false;
                }
            }
            if (doomed)
            {
                for (int j = 0; j < slotPositions.Length; j++)
                {
                    if (adjacentEnemyGroups[i] == enemyGroups[j])
                    {
                        enemyGroups[j] = 0;
                        prisoners++;
                        Destroy(boardStones[j]);
                        boardStones[j] = null;
                    }
                }
            }
        }
        if (player1Turn)
        {
            player1Groups = friendlyGroups;
            player2Groups = enemyGroups;
            player1Prisoners = prisoners;
        }
        else
        {
            player1Groups = enemyGroups;
            player2Groups = friendlyGroups;
            player2Prisoners = prisoners;
        }
        currentStone.SetActive(true);
        currentStone.transform.SetParent(transform);
        boardStones[selectedSlot] = currentStone;
        currentStone = Instantiate(stonePrefab, transform.position, transform.rotation);
        currentStone.transform.localScale = new Vector3(stoneScale, stoneScale, stoneScale);
        player1Turn = !player1Turn;
        SetValidMoves();
    }
}
