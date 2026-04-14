using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Particle_Spawner : MonoBehaviour
{
    public GameObject firepoint;
    public Camera cam;
    public float maximumLenght;
    public float fireRate = 1;
    public GameObject vfx;
    public float autoRemoveVfx = 2;
    

    private GameObject effectToSpawn;
    private GameObject effectDebris;
    private float timeToFire = 0;
    private Ray rayMouse;
    private Vector3 pos;
    private Vector3 direction;
    private Quaternion rotation;

    // Start is called before the first frame update
    void Start()
    {
        effectToSpawn = vfx;
    }

    // Update is called once per frame
    void Update()
    {

        //orient to mouse
        if (cam != null)
        {
            RaycastHit hit;
            var mousePos = Input.mousePosition;
            rayMouse = cam.ScreenPointToRay(mousePos);
            if (Physics.Raycast(rayMouse.origin, rayMouse.direction, out hit, maximumLenght))
            {
                RotateToMouse(gameObject, hit.point);
            }
            else
            {
                var pos = rayMouse.GetPoint(maximumLenght);
                RotateToMouse(gameObject, hit.point);
            }
        }

        //spawn particle on click
        if (Input.GetMouseButton(0) && Time.time >= timeToFire)
        {
            timeToFire = Time.time + 1 / fireRate;
            SpawnVFX();
        }

    }
    void SpawnVFX()
    {
        GameObject vfx;

        vfx = Instantiate(effectToSpawn, firepoint.transform.position, Quaternion.identity);

        vfx.transform.localRotation = rotation;

        Destroy(vfx, autoRemoveVfx);

    }

    void RotateToMouse(GameObject obj, Vector3 destination)
    {
        direction = new Vector3(destination.x, 0, destination.z) - new Vector3(obj.transform.position.x, 0, obj.transform.position.z);
        rotation = Quaternion.LookRotation(direction);
        obj.transform.localRotation = Quaternion.Lerp(obj.transform.rotation, rotation, 1);
    }
}
