var ballX = 120;
var ballY = 100;

var ballVX = 3;
var ballVY = 3;

var paddleY = 100;
var aiPaddleY = 100;

var paddleY2 = 100;
var aiPaddleY2 = 100;

var paddleY3 = 100;
var aiPaddleY3 = 100;

var score1 = 0;
var score1Tile = 0;
var score2 = 0;
var score2Tile = 0;


for(var x = 0; x < 32; x = x + 1) {
	tileAt(x, 2, 128);
	tileAt(x, 26, 128);
}


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
        ballVY = 3;
    }

    if (ballY > 200) {
        ballY = 200;
        ballVY = 0 - 3;
    }


    // --------------------------------
    // PLAYER INPUT
    // --------------------------------

    if (UP1()) {
        if (paddleY > 30) {
            paddleY = paddleY - 5;
        }
    }

    if (DOWN1()) {
        if (paddleY < 185) {
            paddleY = paddleY + 5;
        }
    }


    // --------------------------------
    // SIMPLE AI
    // --------------------------------

    if (ballY < aiPaddleY) {
        if (aiPaddleY > 16) {
            aiPaddleY = aiPaddleY - 2;
        }
    }

    if (ballY > aiPaddleY) {
        if (aiPaddleY < 218) {
            aiPaddleY = aiPaddleY + 2;
        }
    }


    // --------------------------------
    // LEFT PADDLE COLLISION
    // --------------------------------

    if (ballX < 24) {
		var tmp1 = paddleY - 12;
		var tmp2 = paddleY + 12;
        if (ballY > tmp1) {
            if (ballY < tmp2) {
                ballX = 24;
                ballVX = 3;
            }
        }
    }


    // --------------------------------
    // RIGHT PADDLE COLLISION
    // --------------------------------

    if (ballX > 232) {
		tmp1 = aiPaddleY - 12;
		tmp2 = aiPaddleY + 12;
        if (ballY > tmp1) {
            if (ballY < tmp2) {
                ballX = 232;
                ballVX = 0 - 3;
            }
        }
    }


    // --------------------------------
    // RESET IF BALL MISSES A PADDLE
    // --------------------------------
	
    if (ballX > 248) {
		score1 = score1 + 1;
        ballX = 120;
        ballY = 100;
        ballVX = 0 - 3;
        ballVY = 3;
    }

    if (ballX < 5) {
		score2 = score2 + 1;
        ballX = 120;
        ballY = 100;
        ballVX = 3;
        ballVY = 3;
    }

	if (score1 > 9) {
		score1 = 0;
		score2 = 0;
	}

	if (score2 > 9) {
		score1 = 0;
		score2 = 0;
	}



	paddleY2 = paddleY - 8;
	paddleY3 = paddleY + 8;
	aiPaddleY2 = aiPaddleY - 8;
	aiPaddleY3 = aiPaddleY + 8;

	score1Tile = score1 + 16;
	score2Tile = score2 + 16;

	// --------------------------------
    // DRAW PLAYER PADDLE
    // --------------------------------

    sprite(1, 16, paddleY, 128);
	sprite(2, 16, paddleY2, 128);
	sprite(3, 16, paddleY3, 128);
    

    // --------------------------------
    // DRAW AI PADDLE
    // --------------------------------

    sprite(5, 240, aiPaddleY, 128);
	sprite(6, 240, aiPaddleY2, 128);
	sprite(7, 240, aiPaddleY3, 128);
    

    // --------------------------------
    // DRAW BALL
    // --------------------------------

    sprite(8, ballX, ballY, 211);

    // --------------------------------
    // SYNCHRONIZE WITH NEXT FRAME
    // --------------------------------

    sync();

	tileAt(15, 4, score1Tile);
	tileAt(16, 4, 26);
	tileAt(17, 4, score2Tile);
	
	
    
	

}