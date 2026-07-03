//@breif
//ボードのマスを生成するスクリプト

using UnityEngine;

public class BoardGenerator : MonoBehaviour
{
    public GameObject cellPrefab; // 1で作ったPrefabをここにドラッグ
    public int width = 5;
    public int height = 5;
    public  Vector2 cellSize = new Vector2(100f, 100f);

    void Start()
    {
        GenerateBoard();
    }

    void GenerateBoard()
    {
        for (int x = 0; x < 15; x++)
        {
            for (int y = 0; y < 7; y++)
            {
               
                // マスを生成
                GameObject cell = Instantiate(cellPrefab, transform);
                cell.name = $"Cell_{x}_{y}";

                // 座標情報をセット
                CellComponent comp = cell.GetComponent<CellComponent>();
                comp.x = x;
                comp.y = y;

                // 位置をずらして配置
                RectTransform rt = cell.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(x * cellSize.x, y * cellSize.y);
                
            }
        }
    }
}