
// NTOS Main executable file
function init() {
    // This function is called when the project is initialized
}

function draw() {   
    dialog("Pong");
}

draw();

function loop() {
    // This function is called every frame
    var key = getKey();
    switch(key) {
        case '*':
            // Do something when the '*' key is pressed
            break;
        case '#':
            // Do something when the '#' key is pressed
            break; 
    }
    delay(1);
}
