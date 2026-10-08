var ballX = 120;
var ballY = 100;

var ballVX = 5;
var ballVY = 5;

var paddleY = 100;
var aiPaddleY = 100;

var score1 = 0;
var score2 = 0;

while (true) { 

    // --------------------------------
    // BALL MOVEMENT
    // --------------------------------

    ballX = ballX + ballVX;
    ballY = ballY + ballVY;


    // --------------------------------
    // TOP / BOTTOM WALLS
    // --------------------------------

    if (ballY < 20) {
        ballY = 20;
        ballVY = 5;
    }

    if (ballY > 200) {
        ballY = 200;
        ballVY = 0 - 5;
    }


    // --------------------------------
    // PLAYER INPUT
    // --------------------------------

    if (UP1()) {
        if (paddleY > 16) {
            paddleY = paddleY - 5;
        }
    }

    if (DOWN1()) {
        if (paddleY < 218) {
            paddleY = paddleY + 5;
        }
    }


    // --------------------------------
    // SIMPLE AI
    // --------------------------------

    if (ballY < aiPaddleY) {
        if (aiPaddleY > 16) {
            aiPaddleY = aiPaddleY - 3;
        }
    }

    if (ballY > aiPaddleY) {
        if (aiPaddleY < 218) {
            aiPaddleY = aiPaddleY + 3;
        }
    }


    // --------------------------------
    // LEFT PADDLE COLLISION
    // --------------------------------

    if (ballX < 16) {
        if (ballY > paddleY - 12) {
            if (ballY < paddleY + 12) {
                ballX = 16;
                ballVX = 5;
            }
        }
    }


    // --------------------------------
    // RIGHT PADDLE COLLISION
    // --------------------------------

    if (ballX > 232) {
        if (ballY > aiPaddleY - 12) {
            if (ballY < aiPaddleY + 12) {
                ballX = 232;
                ballVX = 0 - 5;
            }
        }
    }


    // --------------------------------
    // RESET IF BALL MISSES A PADDLE
    // --------------------------------

    if (ballX < 0) {
        ballX = 120;
        ballY = 100;
        ballVX = 5;
        ballVY = 5;
    }

    if (ballX > 248) {
        ballX = 120;
        ballY = 100;
        ballVX = 0 - 5;
        ballVY = 5;
    }


    // --------------------------------
    // SYNCHRONIZE WITH NEXT FRAME
    // --------------------------------

    frame();
	tileAt(10, 10, 128);
	tileAt(11, 10, 128);
	
    // --------------------------------
    // DRAW PLAYER PADDLE
    // --------------------------------

    sprite(1, 128, 8, paddleY);
    

    // --------------------------------
    // DRAW AI PADDLE
    // --------------------------------

    sprite(5, 128, 240, aiPaddleY);
    

    // --------------------------------
    // DRAW BALL
    // --------------------------------

    sprite(8, 211, ballX, ballY);
	

}