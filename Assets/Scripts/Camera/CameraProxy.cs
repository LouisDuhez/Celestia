using UnityEngine;

public class CameraProxy : MonoBehaviour
{
    public Transform player; // Glissez votre personnage ici

    // Valeurs de profondeur fixes pour chaque plan
    private float fixedZ;
    private float fixedX;

    void Start()
    {
        // On enregistre la position de départ
        fixedZ = transform.position.z;
        fixedX = transform.position.x;
    }

    void LateUpdate()
    {
        if (player == null) return;

        // On suit le joueur sur X et Y, mais on garde le Z fixe
        // (À adapter selon l'orientation de votre caméra)
        transform.position = new Vector3(player.position.x, player.position.y, fixedZ);
    }
}